using Microsoft.Extensions.Configuration;
using TestIdentity.DataAccess;

namespace TestIdentity.IntegrationTests;

public sealed class ConfigurationValidationTests
{
    [Fact]
    public void RequiredConnectionStringGuard_FailsFast_WhenValueIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = string.Empty
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => configuration.GetRequiredConnectionString("Default"));

        Assert.Contains("Connection string 'Default' is required.", exception.Message, StringComparison.Ordinal);
    }
}
