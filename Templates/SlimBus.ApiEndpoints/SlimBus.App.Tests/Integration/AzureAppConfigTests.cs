namespace SlimBus.App.Tests.Integration;

public class AzureAppConfigTests(WebApplicationFactory<SlimBus.Api.Program> factory)
    : IClassFixture<WebApplicationFactory<SlimBus.Api.Program>>
{
    [Fact]
    public void Application_ShouldStartSuccessfully_WhenAzureAppConfigurationIsDisabled()
    {
        // Arrange & Act - Just creating the client should start the application
        using var client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FeatureManagement:EnableAzureAppConfiguration", "false");
            builder.UseSetting("FeatureManagement:RunDbMigrationWhenAppStart", "false");
            builder.UseSetting("ConnectionStrings:TEMPDb", "Data Source=:memory:");
        }).CreateClient();

        // Assert - If we get here without exceptions, the application started successfully
        client.ShouldNotBeNull();
    }

    [Fact]
    public void Application_ShouldStartSuccessfully_WhenAzureAppConfigurationIsEnabled_ButNoConnectionString()
    {
        // Arrange & Act - This should start successfully and fall back to local configuration
        using var client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FeatureManagement:EnableAzureAppConfiguration", "true");
            builder.UseSetting("FeatureManagement:RunDbMigrationWhenAppStart", "false");
            builder.UseSetting("ConnectionStrings:AzureAppConfiguration", ""); // Empty connection string
            builder.UseSetting("ConnectionStrings:TEMPDb", "Data Source=:memory:");
        }).CreateClient();

        // Assert - If we get here without exceptions, the application started successfully with fallback
        client.ShouldNotBeNull();
    }

    [Fact]
    public void Application_ShouldStartSuccessfully_WhenAzureAppConfigurationIsEnabled_WithInvalidConnectionString()
    {
        // Arrange & Act - This should start successfully and fall back to local configuration
        using var client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FeatureManagement:EnableAzureAppConfiguration", "true");
            builder.UseSetting("FeatureManagement:RunDbMigrationWhenAppStart", "false");
            builder.UseSetting("ConnectionStrings:AzureAppConfiguration", "invalid-connection-string");
            builder.UseSetting("ConnectionStrings:TEMPDb", "Data Source=:memory:");
        }).CreateClient();

        // Assert - If we get here without exceptions, the application started successfully with fallback
        client.ShouldNotBeNull();
    }
}