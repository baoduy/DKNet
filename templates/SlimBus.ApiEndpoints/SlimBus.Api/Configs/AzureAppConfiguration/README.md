# Azure App Configuration Integration

This folder contains the Azure App Configuration integration for SlimBus.Api, providing centralized configuration management and feature flags.

## Overview

Azure App Configuration is a managed service that helps developers centralize their application configuration and feature flags simply and securely. This integration enables the SlimBus.Api application to:

- Load configuration values from Azure App Configuration
- Override local appsettings.json values with remote configuration
- Support feature flags with real-time updates
- Provide environment-specific configuration using labels
- Handle connection failures gracefully with fallback to local configuration

## Files

### AzureAppConfigurationOptions.cs

Defines configuration options for Azure App Configuration integration:

- **ConnectionString**: Azure App Configuration connection string
- **KeyPrefix**: Optional prefix to filter configuration keys
- **Label**: Optional label for environment-specific configuration
- **CacheExpirationInSeconds**: Configuration refresh interval (default: 300 seconds)
- **LoadFeatureFlags**: Whether to load feature flags (default: true)
- **FeatureFlagPrefix**: Optional prefix to filter feature flags

### AzureAppConfigurationSetup.cs

Contains extension methods for configuring Azure App Configuration:

- **AddSlimBusAzureAppConfiguration**: Adds Azure App Configuration as a configuration source
- **AddAzureAppConfigurationServices**: Registers Azure App Configuration services for dependency injection
- Helper methods for configuring key-value retrieval, refresh settings, and feature flags

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "AzureAppConfiguration": "Endpoint=https://your-app-config.azconfig.io;Id=your-id;Secret=your-secret"
  },
  "FeatureManagement": {
    "EnableAzureAppConfiguration": false
  },
  "AzureAppConfiguration": {
    "KeyPrefix": "",
    "Label": "",
    "CacheExpirationInSeconds": 300,
    "LoadFeatureFlags": true,
    "FeatureFlagPrefix": ""
  }
}
```

### Environment Variables (Recommended for Production)

```bash
# Connection string should be stored as environment variable for security
ConnectionStrings__AzureAppConfiguration="Endpoint=https://your-app-config.azconfig.io;Id=your-id;Secret=your-secret"

# Enable the feature
FeatureManagement__EnableAzureAppConfiguration=true

# Environment-specific label
AzureAppConfiguration__Label="Production"
```

## Setup Guide

### 1. Create Azure App Configuration Resource

1. Go to Azure Portal
2. Create a new Azure App Configuration resource
3. Note the connection string from the Access Keys section

### 2. Configure Connection String

**For Development:**
```json
{
  "ConnectionStrings": {
    "AzureAppConfiguration": "Endpoint=https://your-app-config.azconfig.io;Id=your-id;Secret=your-secret"
  }
}
```

**For Production (using environment variables):**
```bash
export ConnectionStrings__AzureAppConfiguration="Endpoint=https://your-app-config.azconfig.io;Id=your-id;Secret=your-secret"
```

### 3. Enable the Feature

Set `FeatureManagement:EnableAzureAppConfiguration` to `true` in your configuration.

### 4. Add Configuration Values to Azure App Configuration

In Azure Portal, navigate to your App Configuration resource and add key-value pairs:

**Configuration Keys:**
- `SlimBus:FeatureManagement:EnableSwagger` = `true`
- `SlimBus:Logging:LogLevel:Default` = `Information`
- `SlimBus:CustomSetting` = `RemoteValue`

**Feature Flags:**
- `SlimBus.EnableNewFeature` = `true`
- `SlimBus.EnableBetaFeatures` = `false`

### 5. Use Labels for Environment-Specific Configuration

Create labels for different environments:
- `Development`
- `Staging`
- `Production`

Configure the label in your appsettings:
```json
{
  "AzureAppConfiguration": {
    "Label": "Development"
  }
}
```

## Usage Examples

### Reading Configuration Values

Configuration values from Azure App Configuration are automatically available through `IConfiguration`:

```csharp
public class MyService
{
    private readonly IConfiguration _configuration;

    public MyService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void DoSomething()
    {
        // This value comes from Azure App Configuration if enabled
        var setting = _configuration["CustomSetting"];
        var nestedSetting = _configuration["SlimBus:FeatureManagement:EnableSwagger"];
    }
}
```

### Using Feature Flags

Feature flags are automatically integrated with .NET Feature Management:

```csharp
public class MyController : ControllerBase
{
    private readonly IFeatureManager _featureManager;

    public MyController(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    public async Task<IActionResult> GetData()
    {
        if (await _featureManager.IsEnabledAsync("SlimBus.EnableNewFeature"))
        {
            // New feature code
            return Ok("New feature enabled!");
        }

        // Legacy code
        return Ok("Standard response");
    }
}
```

### Configuration Precedence

Configuration values are loaded in this order (later sources override earlier ones):

1. appsettings.json
2. appsettings.{Environment}.json
3. Environment variables
4. **Azure App Configuration** (if enabled)
5. Command line arguments

## Error Handling

The integration includes robust error handling:

- **Connection Failures**: Falls back to local configuration if Azure App Configuration is unreachable
- **Invalid Connection Strings**: Logs error and continues with local configuration
- **Network Issues**: Graceful degradation with console logging

## Best Practices

### Security

1. **Never store connection strings in source code**
2. **Use environment variables or Azure Key Vault for connection strings**
3. **Rotate access keys regularly**
4. **Use managed identity in Azure environments**

### Configuration Organization

1. **Use consistent key naming conventions** (e.g., `SlimBus:FeatureName:Setting`)
2. **Group related settings** using prefixes
3. **Use labels for environment-specific values**
4. **Document configuration keys** in your team wiki

### Performance

1. **Set appropriate refresh intervals** (default 300 seconds is usually sufficient)
2. **Use key prefixes** to limit the amount of configuration loaded
3. **Monitor Azure App Configuration usage** to avoid throttling

### Development Workflow

1. **Start with local development** using appsettings.json
2. **Enable Azure App Configuration** when ready to test remote configuration
3. **Use different labels** for different environments
4. **Test feature flag scenarios** thoroughly

## Troubleshooting

### Common Issues

**Issue**: "Azure App Configuration connection string is not provided"
- **Solution**: Ensure the connection string is set in `ConnectionStrings:AzureAppConfiguration`

**Issue**: Configuration values not updating
- **Solution**: Check the refresh interval and ensure the application is running long enough for refresh to occur

**Issue**: Feature flags not working
- **Solution**: Verify that `LoadFeatureFlags` is set to `true` and feature flags are properly created in Azure App Configuration

**Issue**: Environment-specific configuration not loading
- **Solution**: Ensure the `Label` is set correctly and matches the label in Azure App Configuration

### Debugging

Enable detailed logging to troubleshoot issues:

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.Extensions.Configuration.AzureAppConfiguration": "Debug"
    }
  }
}
```

## Integration with Existing Features

The Azure App Configuration integration works seamlessly with existing SlimBus.Api features:

- **Feature Management**: Remote feature flags override local feature flags
- **Configuration Binding**: All existing `IConfiguration` usage continues to work
- **Options Pattern**: Options classes automatically receive updated values
- **Health Checks**: Azure App Configuration health can be monitored
- **Logging**: Configuration changes are logged for audit purposes

## Migration Guide

### From Local Configuration Only

1. **Backup your current appsettings.json**
2. **Create Azure App Configuration resource**
3. **Copy key configuration values** to Azure App Configuration
4. **Enable the feature** with `EnableAzureAppConfiguration: true`
5. **Test thoroughly** in a development environment
6. **Gradually migrate** more configuration values

### From Other Configuration Providers

1. **Identify configuration values** to centralize
2. **Create equivalent keys** in Azure App Configuration
3. **Use labels** for environment-specific values
4. **Enable feature gradually** across environments
5. **Remove old configuration providers** once migration is complete