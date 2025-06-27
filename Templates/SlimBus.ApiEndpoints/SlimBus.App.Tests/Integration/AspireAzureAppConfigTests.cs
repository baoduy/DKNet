using SlimBus.App.Tests.Fixtures;

namespace SlimBus.App.Tests.Integration;

/// <summary>
/// Enhanced Azure App Configuration integration tests using .NET Aspire host
/// This provides more realistic testing with proper infrastructure orchestration
/// </summary>
[Collection(nameof(AzureAppConfigCollectionFixture))]
public class AspireAzureAppConfigTests(AzureAppConfigFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Ensure the Aspire infrastructure is ready
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public void Application_ShouldStartSuccessfully_WithAspireInfrastructure_WhenAzureAppConfigDisabled()
    {
        // Arrange & Act
#pragma warning disable CA2000 // Dispose objects before losing scope - WebApplicationFactory pattern in tests
        using var factory = new WebApplicationFactory<SlimBus.Api.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("FeatureManagement:EnableAzureAppConfiguration", "false");
                builder.UseSetting("FeatureManagement:RunDbMigrationWhenAppStart", "false");
                builder.UseSetting("ConnectionStrings:Redis", fixture.CacheConnectionString);
                builder.UseSetting("ConnectionStrings:AppDb", fixture.DbConnectionString);
            });
#pragma warning restore CA2000

        using var client = factory.CreateClient();

        // Assert
        client.ShouldNotBeNull();
    }

    [Fact]
    public void Application_ShouldStartSuccessfully_WithAspireInfrastructure_WhenAzureAppConfigEnabled_WithMockConnectionString()
    {
        // Arrange & Act - Testing with mock Azure App Configuration
#pragma warning disable CA2000 // Dispose objects before losing scope - WebApplicationFactory pattern in tests
        using var factory = new WebApplicationFactory<SlimBus.Api.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("FeatureManagement:EnableAzureAppConfiguration", "true");
                builder.UseSetting("FeatureManagement:RunDbMigrationWhenAppStart", "false");
                builder.UseSetting("ConnectionStrings:Redis", fixture.CacheConnectionString);
                builder.UseSetting("ConnectionStrings:AppDb", fixture.DbConnectionString);
                builder.UseSetting("ConnectionStrings:AzureAppConfiguration", fixture.AppConfigConnectionString);
                
                // Configure Azure App Configuration options for testing
                builder.UseSetting("AzureAppConfiguration:KeyPrefix", "SlimBusApi:");
                builder.UseSetting("AzureAppConfiguration:Label", "Test");
                builder.UseSetting("AzureAppConfiguration:CacheExpirationInSeconds", "60");
                builder.UseSetting("AzureAppConfiguration:LoadFeatureFlags", "true");
                builder.UseSetting("AzureAppConfiguration:FeatureFlagPrefix", "SlimBusApi");
            });
#pragma warning restore CA2000

        using var client = factory.CreateClient();

        // Assert
        client.ShouldNotBeNull();
    }

    [Fact]
    public void Application_ShouldStartSuccessfully_WithAspireInfrastructure_WhenAzureAppConfigEnabled_WithInvalidConnectionString()
    {
        // Arrange & Act - Testing fallback behavior with invalid connection string
#pragma warning disable CA2000 // Dispose objects before losing scope - WebApplicationFactory pattern in tests
        using var factory = new WebApplicationFactory<SlimBus.Api.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("FeatureManagement:EnableAzureAppConfiguration", "true");
                builder.UseSetting("FeatureManagement:RunDbMigrationWhenAppStart", "false");
                builder.UseSetting("ConnectionStrings:Redis", fixture.CacheConnectionString);
                builder.UseSetting("ConnectionStrings:AppDb", fixture.DbConnectionString);
                builder.UseSetting("ConnectionStrings:AzureAppConfiguration", "invalid-connection-string");
            });
#pragma warning restore CA2000

        using var client = factory.CreateClient();

        // Assert - Application should start successfully with fallback to local configuration
        client.ShouldNotBeNull();
    }

    [Fact]
    public void Application_ShouldStartSuccessfully_WithAspireInfrastructure_WhenAzureAppConfigEnabled_WithEmptyConnectionString()
    {
        // Arrange & Act - Testing fallback behavior with empty connection string
#pragma warning disable CA2000 // Dispose objects before losing scope - WebApplicationFactory pattern in tests
        using var factory = new WebApplicationFactory<SlimBus.Api.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("FeatureManagement:EnableAzureAppConfiguration", "true");
                builder.UseSetting("FeatureManagement:RunDbMigrationWhenAppStart", "false");
                builder.UseSetting("ConnectionStrings:Redis", fixture.CacheConnectionString);
                builder.UseSetting("ConnectionStrings:AppDb", fixture.DbConnectionString);
                builder.UseSetting("ConnectionStrings:AzureAppConfiguration", "");
            });
#pragma warning restore CA2000

        using var client = factory.CreateClient();

        // Assert - Application should start successfully with fallback to local configuration
        client.ShouldNotBeNull();
    }

    [Fact]
    public async Task Application_ShouldServeHealthChecks_WithAspireInfrastructure()
    {
        // Arrange
#pragma warning disable CA2000 // Dispose objects before losing scope - WebApplicationFactory pattern in tests
        using var factory = new WebApplicationFactory<SlimBus.Api.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("FeatureManagement:EnableAzureAppConfiguration", "false");
                builder.UseSetting("FeatureManagement:RunDbMigrationWhenAppStart", "false");
                builder.UseSetting("ConnectionStrings:Redis", fixture.CacheConnectionString);
                builder.UseSetting("ConnectionStrings:AppDb", fixture.DbConnectionString);
            });
#pragma warning restore CA2000

        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.ShouldNotBeNull();
        response.IsSuccessStatusCode.ShouldBeTrue();
    }
}