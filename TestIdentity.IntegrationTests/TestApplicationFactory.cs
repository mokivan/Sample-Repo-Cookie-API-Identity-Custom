using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace TestIdentity.IntegrationTests;

public sealed class TestApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("test_identity")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public string PostgresConnectionString => $"{_postgresContainer.GetConnectionString()};Include Error Detail=true";

    public string RedisConnectionString
    {
        get
        {
            var host = _redisContainer.Hostname;
            var port = _redisContainer.GetMappedPublicPort(6379);
            return $"{host}:{port},name=TestIdentity";
        }
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        await _redisContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = PostgresConnectionString,
                ["ConnectionStrings:RedisConn"] = RedisConnectionString,
                ["Security:AllowSelfAssignedRoles"] = "true",
                ["Security:RequireHttpsForAuthCookie"] = "false"
            });
        });
    }
}
