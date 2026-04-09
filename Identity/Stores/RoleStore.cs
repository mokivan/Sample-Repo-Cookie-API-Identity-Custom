using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TestIdentity.Identity.CustomModel;

namespace TestIdentity.Identity.Stores
{
    public class RoleStore : IRoleStore<AppRole>
    {
        private readonly DataAccess.AppContext _appContext;

        public RoleStore(DataAccess.AppContext appContext)
        {
            _appContext = appContext;
        }

        public async Task<IdentityResult> CreateAsync(AppRole role, CancellationToken cancellationToken)
        {
            try
            {
                _appContext.Roles.Add(role);
                await _appContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                return IdentityResult.Failed(CreateError(ex, "Unable to create role."));
            }

            return IdentityResult.Success;
        }

        public async Task<IdentityResult> DeleteAsync(AppRole role, CancellationToken cancellationToken)
        {
            try
            {
                _appContext.Roles.Remove(role);
                await _appContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                return IdentityResult.Failed(CreateError(ex, "Unable to delete role."));
            }

            return IdentityResult.Success;
        }

        public void Dispose()
        {
        }

        public async Task<AppRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
        {
            return int.TryParse(roleId, out var parsedRoleId)
                ? await _appContext.Roles.SingleOrDefaultAsync(role => role.Id == parsedRoleId, cancellationToken)
                : null;
        }

        public Task<AppRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            var normalized = Normalize(normalizedRoleName);
            return _appContext.Roles.SingleOrDefaultAsync(role => role.NormalizedName == normalized, cancellationToken);
        }

        public Task<string?> GetNormalizedRoleNameAsync(AppRole role, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(role.NormalizedName);
        }

        public Task<string> GetRoleIdAsync(AppRole role, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(role.Id.ToString());
        }

        public Task<string?> GetRoleNameAsync(AppRole role, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(role.Name);
        }

        public Task SetNormalizedRoleNameAsync(AppRole role, string? normalizedName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            role.NormalizedName = Normalize(normalizedName);
            return Task.CompletedTask;
        }

        public Task SetRoleNameAsync(AppRole role, string? roleName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(roleName);
            cancellationToken.ThrowIfCancellationRequested();
            role.Name = roleName;
            return Task.CompletedTask;
        }

        public async Task<IdentityResult> UpdateAsync(AppRole role, CancellationToken cancellationToken)
        {
            try
            {
                _appContext.Roles.Update(role);
                await _appContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                return IdentityResult.Failed(CreateError(ex, "Unable to update role."));
            }

            return IdentityResult.Success;
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
