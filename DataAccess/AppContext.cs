using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TestIdentity.Identity.CustomModel;

namespace TestIdentity.DataAccess
{
    public class AppContext : DbContext
    {
        public AppContext(DbContextOptions<AppContext> options)
            : base(options)
        {
        }

        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<AppRole> Roles => Set<AppRole>();
        public DbSet<AppPermission> Permissions => Set<AppPermission>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var permissionEntity = modelBuilder.Entity<AppPermission>();
            permissionEntity.ToTable("permissions");
            permissionEntity.HasKey(permission => permission.Id);
            permissionEntity.Property(permission => permission.Name).HasMaxLength(128).IsRequired();
            permissionEntity.HasIndex(permission => new { permission.RoleId, permission.Name }).IsUnique();
            permissionEntity.HasOne(permission => permission.Role)
                .WithMany(role => role.Permissions)
                .HasForeignKey(permission => permission.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            var roleEntity = modelBuilder.Entity<AppRole>();
            roleEntity.ToTable("roles");
            roleEntity.HasKey(role => role.Id);
            roleEntity.Property(role => role.Name).HasMaxLength(128).IsRequired();
            roleEntity.Property(role => role.NormalizedName).HasMaxLength(128).IsRequired();
            roleEntity.Property(role => role.Description).HasMaxLength(256).IsRequired();
            roleEntity.HasIndex(role => role.NormalizedName).IsUnique();
            roleEntity.HasMany(role => role.Permissions)
                .WithOne(permission => permission.Role);

            var userEntity = modelBuilder.Entity<AppUser>();
            userEntity.ToTable("users");
            userEntity.HasKey(user => user.Id);
            userEntity.Property(user => user.Name).HasMaxLength(128).IsRequired();
            userEntity.Property(user => user.Email).HasMaxLength(256).IsRequired();
            userEntity.Property(user => user.NormalizedEmail).HasMaxLength(256).IsRequired();
            userEntity.Property(user => user.Username).HasMaxLength(128).IsRequired();
            userEntity.Property(user => user.NormalizedUsername).HasMaxLength(128).IsRequired();
            userEntity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
            userEntity.HasIndex(user => user.NormalizedEmail).IsUnique();
            userEntity.HasIndex(user => user.NormalizedUsername).IsUnique();
            userEntity.HasMany(user => user.Roles).WithMany(role => role.Users);
            userEntity.Ignore(user => user.Permissions);

            roleEntity.HasData(AppRole.SeedRoles);
            permissionEntity.HasData(AppPermission.SeedPermissions);
        }
    }

    public static class AppContextInstaller
    {
        public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration, string connectionStringName)
        {
            var connectionString = configuration.GetRequiredConnectionString(connectionStringName);

            services.AddDbContext<AppContext>(options =>
            {
                options.UseNpgsql(connectionString)
                    .UseSnakeCaseNamingConvention();
            });

            return services;
        }

        public static string GetRequiredConnectionString(this IConfiguration configuration, string connectionStringName)
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{connectionStringName}' is required.");
            }

            return connectionString;
        }
    }

    public class DesignTimeAppContextFactory : IDesignTimeDbContextFactory<AppContext>
    {
        public AppContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddJsonFile("appsettings.Local.json", optional: true)
                .AddEnvironmentVariables("TestIdentity_")
                .Build();

            var connectionString = configuration.GetConnectionString("Default")
                ?? "Host=localhost;Port=5432;Database=test_identity;Username=postgres;Password=postgres";

            var optionsBuilder = new DbContextOptionsBuilder<AppContext>();
            optionsBuilder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();

            return new AppContext(optionsBuilder.Options);
        }
    }
}
