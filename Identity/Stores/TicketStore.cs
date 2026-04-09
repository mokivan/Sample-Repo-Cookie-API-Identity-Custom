using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace TestIdentity.Identity.Stores
{
    public class TicketStore : ITicketStore, ICustomSessionStore
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

        public const string SessionIdClaimType = ClaimTypes.Sid;
        public const string SessionIdPropertyName = "session_id";

        private readonly IDistributedCache _cache;
        private readonly ILogger<TicketStore> _logger;
        private readonly IOptionsMonitor<CookieAuthenticationOptions> _cookieOptionsMonitor;
        private readonly TimeProvider _timeProvider;

        public TicketStore(
            IDistributedCache cache,
            ILogger<TicketStore> logger,
            IOptionsMonitor<CookieAuthenticationOptions> cookieOptionsMonitor,
            TimeProvider timeProvider)
        {
            _cache = cache;
            _logger = logger;
            _cookieOptionsMonitor = cookieOptionsMonitor;
            _timeProvider = timeProvider;
        }

        public async Task RemoveAsync(string key)
        {
            var ticket = await RetrieveAsync(key);
            await _cache.RemoveAsync(GetTicketCacheKey(key));

            if (ticket is null)
            {
                return;
            }

            var username = Normalize(ticket.Principal.Identity?.Name);
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            var sessionIds = await GetSessionIdIndexAsync(username);
            if (sessionIds.Remove(key))
            {
                await PersistSessionIdIndexAsync(username, sessionIds, null);
            }
        }

        public async Task RenewAsync(string key, AuthenticationTicket ticket)
        {
            var sessionId = EnsureSessionId(ticket, key);
            var entryOptions = CreateCacheEntryOptions(ticket);
            var serializedTicket = TicketSerializer.Default.Serialize(ticket);
            await _cache.SetAsync(GetTicketCacheKey(sessionId), serializedTicket, entryOptions);

            var username = Normalize(ticket.Principal.Identity?.Name);
            if (!string.IsNullOrWhiteSpace(username))
            {
                var sessionIds = await GetSessionIdIndexAsync(username);
                sessionIds.Add(sessionId);
                await PersistSessionIdIndexAsync(username, sessionIds, entryOptions);
            }
        }

        public async Task<AuthenticationTicket?> RetrieveAsync(string key)
        {
            var serializedTicket = await _cache.GetAsync(GetTicketCacheKey(key));
            return serializedTicket is null ? null : TicketSerializer.Default.Deserialize(serializedTicket);
        }

        public async Task<string> StoreAsync(AuthenticationTicket ticket)
        {
            var sessionId = EnsureSessionId(ticket);
            var entryOptions = CreateCacheEntryOptions(ticket);
            var serializedTicket = TicketSerializer.Default.Serialize(ticket);
            await _cache.SetAsync(GetTicketCacheKey(sessionId), serializedTicket, entryOptions);

            var username = Normalize(ticket.Principal.Identity?.Name);
            if (!string.IsNullOrWhiteSpace(username))
            {
                var sessionIds = await GetSessionIdIndexAsync(username);
                sessionIds.Add(sessionId);
                await PersistSessionIdIndexAsync(username, sessionIds, entryOptions);
            }

            return sessionId;
        }

        public async Task<IReadOnlyCollection<AuthenticationTicket>> GetSessionsAsync(string username, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedUsername = Normalize(username);
            var sessionIds = await GetSessionIdIndexAsync(normalizedUsername);
            if (sessionIds.Count == 0)
            {
                return Array.Empty<AuthenticationTicket>();
            }

            var sessions = new List<AuthenticationTicket>();
            var staleSessionIds = new List<string>();

            foreach (var sessionId in sessionIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var ticket = await RetrieveAsync(sessionId);
                if (ticket is null)
                {
                    staleSessionIds.Add(sessionId);
                    continue;
                }

                sessions.Add(ticket);
            }

            if (staleSessionIds.Count > 0)
            {
                foreach (var staleSessionId in staleSessionIds)
                {
                    sessionIds.Remove(staleSessionId);
                }

                await PersistSessionIdIndexAsync(normalizedUsername, sessionIds, null);
            }

            _logger.LogInformation("Retrieved {SessionCount} active sessions for {Username}.", sessions.Count, normalizedUsername);
            return sessions
                .OrderByDescending(ticket => ticket.Properties.IssuedUtc ?? DateTimeOffset.MinValue)
                .ToArray();
        }

        public async Task<bool> RemoveOwnedSessionAsync(string username, string sessionId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedUsername = Normalize(username);
            var normalizedSessionId = sessionId.Trim();
            if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(normalizedSessionId))
            {
                return false;
            }

            var sessionIds = await GetSessionIdIndexAsync(normalizedUsername);
            if (!sessionIds.Contains(normalizedSessionId))
            {
                return false;
            }

            await RemoveAsync(normalizedSessionId);
            return true;
        }

        public async Task RemoveAllAsync(string username, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedUsername = Normalize(username);
            var sessionIds = await GetSessionIdIndexAsync(normalizedUsername);
            foreach (var sessionId in sessionIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _cache.RemoveAsync(GetTicketCacheKey(sessionId), cancellationToken);
            }

            await _cache.RemoveAsync(GetUserSessionsCacheKey(normalizedUsername), cancellationToken);
        }

        private async Task<HashSet<string>> GetSessionIdIndexAsync(string normalizedUsername)
        {
            if (string.IsNullOrWhiteSpace(normalizedUsername))
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var serializedSessionIds = await _cache.GetStringAsync(GetUserSessionsCacheKey(normalizedUsername));
            return string.IsNullOrWhiteSpace(serializedSessionIds)
                ? new HashSet<string>(StringComparer.Ordinal)
                : JsonSerializer.Deserialize<HashSet<string>>(serializedSessionIds, JsonSerializerOptions) ?? new HashSet<string>(StringComparer.Ordinal);
        }

        private async Task PersistSessionIdIndexAsync(
            string normalizedUsername,
            HashSet<string> sessionIds,
            DistributedCacheEntryOptions? entryOptions)
        {
            if (string.IsNullOrWhiteSpace(normalizedUsername))
            {
                return;
            }

            if (sessionIds.Count == 0)
            {
                await _cache.RemoveAsync(GetUserSessionsCacheKey(normalizedUsername));
                return;
            }

            var effectiveEntryOptions = entryOptions ?? CreateUserIndexEntryOptions();
            var serializedSessionIds = JsonSerializer.Serialize(sessionIds, JsonSerializerOptions);
            await _cache.SetStringAsync(GetUserSessionsCacheKey(normalizedUsername), serializedSessionIds, effectiveEntryOptions);
        }

        private DistributedCacheEntryOptions CreateCacheEntryOptions(AuthenticationTicket ticket)
        {
            var now = _timeProvider.GetUtcNow();
            var cookieOptions = _cookieOptionsMonitor.Get(IdentityConstants.ApplicationScheme);
            var expiresUtc = ticket.Properties.ExpiresUtc
                ?? ticket.Properties.IssuedUtc?.Add(cookieOptions.ExpireTimeSpan)
                ?? now.Add(cookieOptions.ExpireTimeSpan);

            return new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = expiresUtc
            };
        }

        private DistributedCacheEntryOptions CreateUserIndexEntryOptions()
        {
            var cookieOptions = _cookieOptionsMonitor.Get(IdentityConstants.ApplicationScheme);
            return new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = _timeProvider.GetUtcNow().Add(cookieOptions.ExpireTimeSpan)
            };
        }

        private static string EnsureSessionId(AuthenticationTicket ticket, string? requestedSessionId = null)
        {
            var sessionId = requestedSessionId;
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                sessionId = ticket.Principal.FindFirstValue(SessionIdClaimType);
            }

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                sessionId = Guid.NewGuid().ToString("N");
            }

            ticket.Properties.SetString(SessionIdPropertyName, sessionId);

            var identity = ticket.Principal.Identities.FirstOrDefault() ?? new ClaimsIdentity();
            var existingClaim = identity.FindFirst(SessionIdClaimType);
            if (existingClaim is not null)
            {
                identity.RemoveClaim(existingClaim);
            }

            identity.AddClaim(new Claim(SessionIdClaimType, sessionId));
            if (!ticket.Principal.Identities.Contains(identity))
            {
                ticket.Principal.AddIdentity(identity);
            }

            return sessionId;
        }

        private static string GetTicketCacheKey(string sessionId)
        {
            return $"auth:ticket:{sessionId}";
        }

        private static string GetUserSessionsCacheKey(string normalizedUsername)
        {
            return $"auth:user-sessions:{normalizedUsername}";
        }

        private static string Normalize(string? value)
        {
            return value?.Trim().ToUpperInvariant() ?? string.Empty;
        }
    }
}
