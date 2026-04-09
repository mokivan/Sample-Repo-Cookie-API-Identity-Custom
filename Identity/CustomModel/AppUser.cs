using System.Security.Claims;

namespace TestIdentity.Identity.CustomModel
{
    public class AppUser
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NormalizedEmail { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
        public string Username { get; set; } = string.Empty;
        public string NormalizedUsername { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public List<AppRole> Roles { get; set; } = new();

        public IReadOnlyList<Claim> Permissions =>
            Roles
                .SelectMany(role => role.Permissions)
                .Select(permission => permission.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(permission => new Claim(AppClaimTypes.Permission, permission))
                .ToList();
    }

    public static class AppClaimTypes
    {
        public const string Permission = "Permission";
    }
}
