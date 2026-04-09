namespace TestIdentity.Identity.CustomModel
{
    public class AppRole
    {
        public static readonly List<AppRole> SeedRoles = new()
        {
            new ()
            {
                Id = 1,
                Name = "Admin",
                NormalizedName = "ADMIN",
                Description = "Admin user"
            },
            new ()
            {
                Id = 2,
                Name = "Superuser",
                NormalizedName = "SUPERUSER",
                Description = "Superuser"
            }
        };

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string NormalizedName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public List<AppUser> Users { get; set; } = new();
        public List<AppPermission> Permissions { get; set; } = new();
    }
}
