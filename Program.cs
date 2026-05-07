using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using TestIdentity.Configuration;
using TestIdentity.DataAccess;
using TestIdentity.Identity.CustomModel;
using TestIdentity.Identity.Filters;
using TestIdentity.Identity.Stores;

namespace TestIdentity
{
    public class Program
    {
        private const string ApplicationName = "TestIdentity";

        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration.AddEnvironmentVariables($"{typeof(Program).Namespace}_");

            var securityOptions = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();

            builder.Services.AddProblemDetails();
            builder.Services.AddControllers();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services
                .AddOptions<SecurityOptions>()
                .Bind(builder.Configuration.GetSection(SecurityOptions.SectionName))
                .Validate(SecurityOptions.IsValid, "Security configuration contains an invalid trusted proxy or trusted network entry.")
                .ValidateOnStart();

            var redisConnectionString = builder.Configuration.GetRequiredConnectionString("RedisConn");
            var redis = ConnectionMultiplexer.Connect(redisConnectionString);

            builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
            });

            builder.Services
                .AddDataProtection()
                .SetApplicationName(ApplicationName)
                .PersistKeysToStackExchangeRedis(redis, $"data-protection-keys:{ApplicationName}");

            builder.Services.AddSingleton<ITicketStore, TicketStore>();
            builder.Services.AddSingleton<ICustomSessionStore>(services => (ICustomSessionStore)services.GetRequiredService<ITicketStore>());

            builder.Services
                .AddIdentity<AppUser, AppRole>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                    options.Password.RequiredLength = 12;
                    options.Password.RequireDigit = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireNonAlphanumeric = true;
                })
                .AddUserStore<UserStore>()
                .AddRoleStore<RoleStore>();

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                SecurityOptions.ApplyForwardingConfiguration(options, securityOptions);
            });

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.Name = "sample_identity";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = securityOptions.RequireHttpsForAuthCookie
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

            builder.Services
                .AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
                .Configure<ITicketStore>((options, store) => options.SessionStore = store);

            builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
            builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
            builder.Services.AddAuthorization();
            builder.Services.AddDatabase(builder.Configuration, "Default");

            var app = builder.Build();

            app.UseExceptionHandler(exceptionApp =>
            {
                exceptionApp.Run(async context =>
                {
                    await Results.Problem(
                        statusCode: StatusCodes.Status500InternalServerError,
                        title: "An unexpected error occurred.")
                        .ExecuteAsync(context);
                });
            });

            app.UseForwardedHeaders();
            if (securityOptions.ExposeMachineDebugHeaders)
            {
                app.Use(async (context, next) =>
                {
                    context.Response.Headers["X-Machine-Name"] = Environment.MachineName;
                    context.Response.Headers["X-Machine-Ip"] = GetMachineIpAddress();
                    await next();
                });
            }

            if (!app.Environment.IsProduction())
            {
                await ApplyMigrationsAsync(app);
            }

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            await app.RunAsync();
        }

        private static async Task ApplyMigrationsAsync(IHost app)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TestIdentity.DataAccess.AppContext>();
            await context.Database.MigrateAsync();
        }

        private static string GetMachineIpAddress()
        {
            var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
            var address = hostEntry.AddressList.FirstOrDefault(ipAddress => ipAddress.AddressFamily == AddressFamily.InterNetwork);
            return address?.ToString() ?? IPAddress.Loopback.ToString();
        }
    }
}
