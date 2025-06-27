using SlimBus.App.Tests.Fixtures;

namespace SlimBus.App.Tests.Integration;

[Collection(nameof(ShareInfraCollectionFixture))]
public class AzureAppConfigTests(ShareInfraFixture fixture)
{
    [Fact]
    public void Application_ShouldStartSuccessfully_WhenAzureAppConfigurationIsDisabled()
    {
        // Arrange & Act - Just creating the client should start the application
#pragma warning disable CA2000 // Dispose objects before losing scope - WebApplicationFactory pattern in tests
        using var factory = new WebApplicationFactory<SlimBus.Api.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("FeatureManagement:EnableAzureAppConfiguration", "false");
                builder.UseSetting("FeatureManagement:RunDbMigrationWhenAppStart", "false");
                builder.UseSetting("ConnectionStrings:Redis", fixture.CacheConn);
                builder.UseSetting("ConnectionStrings:AppDb", fixture.DbConn);
            });
#pragma warning restore CA2000

        using var client = factory.CreateClient();

        // Assert - If we get here without exceptions, the application started successfully
        client.ShouldNotBeNull();
    }

    [Fact]
    public void Application_ShouldStartSuccessfully_WhenAzureAppConfigurationIsEnabled_ButNoConnectionString()
    {
        // Arrange & Act - This should start successfully and fall back to local configuration
#pragma warning disable CA2000 // Dispose objects before losing scope - WebApplicationFactory pattern in tests
        using var factory = new WebApplicationFactory<SlimBus.Api.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("FeatureManagement:EnableAzureAppConfiguration", "true");
                builder.UseSetting("FeatureManagement:RunDbMigrationWhenAppStart", "false");
                builder.UseSetting("ConnectionStrings:AzureAppConfiguration", ""); // Empty connection string
                builder.UseSetting("ConnectionStrings:Redis", fixture.CacheConn);
                builder.UseSetting("ConnectionStrings:AppDb", fixture.DbConn);
            });
#pragma warning restore CA2000

        using var client = factory.CreateClient();

        // Assert - If we get here without exceptions, the application started successfully with fallback
        client.ShouldNotBeNull();
    }

    [Fact]
    public void Application_ShouldStartSuccessfully_WhenAzureAppConfigurationIsEnabled_WithInvalidConnectionString()
    {
        // Arrange & Act - This should start successfully and fall back to local configuration
#pragma warning disable CA2000 // Dispose objects before losing scope - WebApplicationFactory pattern in tests
        using var factory = new WebApplicationFactory<SlimBus.Api.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("FeatureManagement:EnableAzureAppConfiguration", "true");
                builder.UseSetting("FeatureManagement:RunDbMigrationWhenAppStart", "false");
                builder.UseSetting("ConnectionStrings:AzureAppConfiguration", "invalid-connection-string");
                builder.UseSetting("ConnectionStrings:Redis", fixture.CacheConn);
                builder.UseSetting("ConnectionStrings:AppDb", fixture.DbConn);
            });
#pragma warning restore CA2000

        using var client = factory.CreateClient();

        // Assert - If we get here without exceptions, the application started successfully with fallback
        client.ShouldNotBeNull();
    }
}