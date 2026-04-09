using Microsoft.AspNetCore.Authentication;

namespace TestIdentity.Identity.Stores
{
    public interface ICustomSessionStore
    {
        Task<IReadOnlyCollection<AuthenticationTicket>> GetSessionsAsync(string username, CancellationToken cancellationToken = default);
        Task<bool> RemoveOwnedSessionAsync(string username, string sessionId, CancellationToken cancellationToken = default);
        Task RemoveAllAsync(string username, CancellationToken cancellationToken = default);
    }
}
