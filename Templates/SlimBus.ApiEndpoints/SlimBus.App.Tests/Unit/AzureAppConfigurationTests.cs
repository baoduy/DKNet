
using SlimBus.Api.Configs.AzureAppConfig;
#pragma warning disable IDE0005
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

#pragma warning restore IDE0005

namespace SlimBus.App.Tests.Unit;

public class AzureAppConfigurationTests
{
    [Fact]
    public void AzureAppConfigurationOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new AzureAppConfigOptions();

        // Assert
        options.ConnectionString.ShouldBe(string.Empty);
        options.KeyPrefix.ShouldBe(string.Empty);
        options.Label.ShouldBe(string.Empty);
        options.CacheExpirationInSeconds.ShouldBe(300);
        options.LoadFeatureFlags.ShouldBeTrue();
        options.FeatureFlagPrefix.ShouldBe(string.Empty);
    }

    [Fact]
    public void AzureAppConfigurationOptions_Name_ShouldReturnCorrectValue()
    {
        // Act
        var name = AzureAppConfigOptions.Name;

        // Assert
        name.ShouldBe("AzureAppConfiguration");
    }

    [Fact]
    public void AddSlimBusAzureAppConfiguration_WithEmptyConnectionString_ShouldReturnBuilderWithoutChanges()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();

        var builder = new ConfigurationBuilder();
        var initialSourceCount = builder.Sources.Count;

        // Act
        var result = builder.AddAzureAppConfig("", new AzureAppConfigOptions());

        // Assert
        result.ShouldBe(builder);
        builder.Sources.Count.ShouldBe(initialSourceCount);
    }

    [Fact]
    public void AddSlimBusAzureAppConfiguration_WithNullConnectionString_ShouldReturnBuilderWithoutChanges()
    {
        // Arrange
        var builder = new ConfigurationBuilder();
        var initialSourceCount = builder.Sources.Count;

        // Act
        var result = builder.AddAzureAppConfig(null!, new AzureAppConfigOptions());

        // Assert
        result.ShouldBe(builder);
        builder.Sources.Count.ShouldBe(initialSourceCount);
    }

    [Fact]
    public void AddSlimBusAzureAppConfiguration_WithWhitespaceConnectionString_ShouldReturnBuilderWithoutChanges()
    {
        // Arrange
        var builder = new ConfigurationBuilder();
        var initialSourceCount = builder.Sources.Count;

        // Act
        var result = builder.AddAzureAppConfig("   ", new AzureAppConfigOptions());

        // Assert
        result.ShouldBe(builder);
        builder.Sources.Count.ShouldBe(initialSourceCount);
    }

    [Fact]
    public void AddAzureAppConfigurationServices_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["AzureAppConfiguration:KeyPrefix"] = "Test:",
                ["AzureAppConfiguration:Label"] = "Development",
                ["AzureAppConfiguration:CacheExpirationInSeconds"] = "600",
                ["AzureAppConfiguration:LoadFeatureFlags"] = "false",
                ["AzureAppConfiguration:FeatureFlagPrefix"] = "Test"
            })
            .Build();

        // Act
        var result = services.AddAzureAppConfigServices(configuration);

        // Assert
        result.ShouldBe(services);
        
        // Verify that services are registered
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<IOptions<AzureAppConfigOptions>>();
        options.ShouldNotBeNull();
        
        var optionsValue = options.Value;
        optionsValue.KeyPrefix.ShouldBe("Test:");
        optionsValue.Label.ShouldBe("Development");
        optionsValue.CacheExpirationInSeconds.ShouldBe(600);
        optionsValue.LoadFeatureFlags.ShouldBeFalse();
        optionsValue.FeatureFlagPrefix.ShouldBe("Test");
    }

    [Fact]
    public void AzureAppConfigurationOptions_SetProperties_ShouldWork()
    {
        // Arrange & Act
        var options = new AzureAppConfigOptions
        {
            ConnectionString = "test-connection-string",
            KeyPrefix = "TestApp:",
            Label = "Production",
            CacheExpirationInSeconds = 600,
            LoadFeatureFlags = false,
            FeatureFlagPrefix = "TestApp"
        };

        // Assert
        options.ConnectionString.ShouldBe("test-connection-string");
        options.KeyPrefix.ShouldBe("TestApp:");
        options.Label.ShouldBe("Production");
        options.CacheExpirationInSeconds.ShouldBe(600);
        options.LoadFeatureFlags.ShouldBeFalse();
        options.FeatureFlagPrefix.ShouldBe("TestApp");
    }
}