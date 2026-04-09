using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TestIdentity.Identity.CustomModel;

namespace TestIdentity.Identity.Stores
{
    public class UserStore : IUserStore<AppUser>, IUserRoleStore<AppUser>, IUserPasswordStore<AppUser>, IUserEmailStore<AppUser>
    {
        private readonly DataAccess.AppContext _appContext;

        public UserStore(DataAccess.AppContext appContext)
        {
            _appContext = appContext;
        }

        public async Task AddToRoleAsync(AppUser user, string roleName, CancellationToken cancellationToken)
        {
            var existingUser = await LoadUserWithRolesAsync(user.Id, cancellationToken);
            var role = await _appContext.Roles.SingleOrDefaultAsync(existingRole => existingRole.NormalizedName == Normalize(roleName), cancellationToken);
            if (existingUser is null || role is null)
            {
                throw new InvalidOperationException("Unable to associate the user with the requested role.");
            }

            if (existingUser.Roles.All(existingRole => existingRole.Id != role.Id))
            {
                existingUser.Roles.Add(role);
                await _appContext.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<IdentityResult> CreateAsync(AppUser user, CancellationToken cancellationToken)
        {
            try
            {
                user.Roles = await ResolveRolesAsync(user.Roles.Select(role => role.Id), cancellationToken);
                _appContext.Users.Add(user);
                await _appContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                return IdentityResult.Failed(CreateError(ex, "Unable to create user."));
            }

            return IdentityResult.Success;
        }

        public async Task<IdentityResult> DeleteAsync(AppUser user, CancellationToken cancellationToken)
        {
            var existingUser = await LoadUserWithRolesAsync(user.Id, cancellationToken);
            if (existingUser is null)
            {
                return IdentityResult.Success;
            }

            try
            {
                _appContext.Users.Remove(existingUser);
                await _appContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                return IdentityResult.Failed(CreateError(ex, "Unable to delete user."));
            }

            return IdentityResult.Success;
        }

        public void Dispose()
        {
        }

        public async Task<AppUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            return int.TryParse(userId, out var parsedUserId)
                ? await LoadUserWithRolesAsync(parsedUserId, cancellationToken)
                : null;
        }

        public async Task<AppUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            var normalizedLookup = Normalize(normalizedUserName);
            return await _appContext.Users
                .Include(user => user.Roles)
                .ThenInclude(role => role.Permissions)
                .SingleOrDefaultAsync(
                    user => user.NormalizedUsername == normalizedLookup || user.NormalizedEmail == normalizedLookup,
                    cancellationToken);
        }

        public Task<string?> GetNormalizedUserNameAsync(AppUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(user.NormalizedUsername);
        }

        public Task<string?> GetPasswordHashAsync(AppUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(user.PasswordHash);
        }

        public Task<IList<string>> GetRolesAsync(AppUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IList<string> result = user.Roles.Select(role => role.Name).ToList();
            return Task.FromResult(result);
        }

        public Task<string> GetUserIdAsync(AppUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.Id.ToString());
        }

        public Task<string?> GetUserNameAsync(AppUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(user.Username);
        }

        public async Task<IList<AppUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
        {
            var normalizedRoleName = Normalize(roleName);
            return await _appContext.Users
                .Include(user => user.Roles)
                .ThenInclude(role => role.Permissions)
                .Where(user => user.Roles.Any(role => role.NormalizedName == normalizedRoleName))
                .ToListAsync(cancellationToken);
        }

        public Task<bool> HasPasswordAsync(AppUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(!string.IsNullOrWhiteSpace(user.PasswordHash));
        }

        public Task<bool> IsInRoleAsync(AppUser user, string roleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedRoleName = Normalize(roleName);
            return Task.FromResult(user.Roles.Any(role => role.NormalizedName == normalizedRoleName));
        }

        public async Task RemoveFromRoleAsync(AppUser user, string roleName, CancellationToken cancellationToken)
        {
            var existingUser = await LoadUserWithRolesAsync(user.Id, cancellationToken);
            if (existingUser is null)
            {
                return;
            }

            var normalizedRoleName = Normalize(roleName);
            var role = existingUser.Roles.SingleOrDefault(existingRole => existingRole.NormalizedName == normalizedRoleName);
            if (role is null)
            {
                return;
            }

            existingUser.Roles.Remove(role);
            await _appContext.SaveChangesAsync(cancellationToken);
        }

        public Task SetNormalizedUserNameAsync(AppUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.NormalizedUsername = Normalize(normalizedName);
            return Task.CompletedTask;
        }

        public Task SetPasswordHashAsync(AppUser user, string? passwordHash, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.PasswordHash = passwordHash ?? string.Empty;
            return Task.CompletedTask;
        }

        public Task SetUserNameAsync(AppUser user, string? userName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(userName);
            cancellationToken.ThrowIfCancellationRequested();
            user.Username = userName;
            return Task.CompletedTask;
        }

        public async Task<IdentityResult> UpdateAsync(AppUser user, CancellationToken cancellationToken)
        {
            var existingUser = await LoadUserWithRolesAsync(user.Id, cancellationToken);
            if (existingUser is null)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = nameof(AppUser),
                    Description = "User was not found."
                });
            }

            try
            {
                existingUser.Name = user.Name;
                existingUser.Email = user.Email;
                existingUser.NormalizedEmail = user.NormalizedEmail;
                existingUser.EmailConfirmed = user.EmailConfirmed;
                existingUser.Username = user.Username;
                existingUser.NormalizedUsername = user.NormalizedUsername;
                existingUser.PasswordHash = user.PasswordHash;
                existingUser.Roles = await ResolveRolesAsync(user.Roles.Select(role => role.Id), cancellationToken);
                await _appContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                return IdentityResult.Failed(CreateError(ex, "Unable to update user."));
            }

            return IdentityResult.Success;
        }

        public Task SetEmailAsync(AppUser user, string? email, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.Email = email ?? string.Empty;
            return Task.CompletedTask;
        }

        public Task<string?> GetEmailAsync(AppUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(user.Email);
        }

        public Task<bool> GetEmailConfirmedAsync(AppUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.EmailConfirmed);
        }

        public Task SetEmailConfirmedAsync(AppUser user, bool confirmed, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.EmailConfirmed = confirmed;
            return Task.CompletedTask;
        }

        public Task<AppUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            var normalizedLookup = Normalize(normalizedEmail);
            return _appContext.Users
                .Include(user => user.Roles)
                .ThenInclude(role => role.Permissions)
                .SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedLookup, cancellationToken);
        }

        public Task<string?> GetNormalizedEmailAsync(AppUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(user.NormalizedEmail);
        }

        public Task SetNormalizedEmailAsync(AppUser user, string? normalizedEmail, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.NormalizedEmail = Normalize(normalizedEmail);
            return Task.CompletedTask;
        }

        private async Task<AppUser?> LoadUserWithRolesAsync(int userId, CancellationToken cancellationToken)
        {
            return await _appContext.Users
                .Include(user => user.Roles)
                .ThenInclude(role => role.Permissions)
                .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        }

        private async Task<List<AppRole>> ResolveRolesAsync(IEnumerable<int> roleIds, CancellationToken cancellationToken)
        {
            var distinctRoleIds = roleIds.Where(roleId => roleId > 0).Distinct().ToArray();
            if (distinctRoleIds.Length == 0)
            {
                return new List<AppRole>();
            }

            return await _appContext.Roles.Where(role => distinctRoleIds.Contains(role.Id)).ToListAsync(cancellationToken);
        }

        private static IdentityError CreateError(Exception exception, string description)
        {
            return new IdentityError
            {
                Code = exception.GetType().Name,
                Description = description
            };
        }

        private static string Normalize(string? value)
        {
            return value?.Trim().ToUpperInvariant() ?? string.Empty;
        }
    }
}
