namespace TestIdentity.Identity.CustomModel
{
    public class AppPermission
    {
        public static readonly List<AppPermission> SeedPermissions = new()
        {
            new ()
            {
                Id = 1,
                Name = "ReadSingleForecast",
                RoleId = 1
            },
            new ()
            {
                Id = 2,
                Name = "CreateForecast",
                RoleId = 1
            },
        };

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public int RoleId { get; set; }
        public AppRole Role { get; set; } = null!;
    }
}
