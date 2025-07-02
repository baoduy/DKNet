# Azure App Configuration Integration with .NET Aspire

This document outlines the enhanced integration tests for Azure App Configuration using .NET Aspire host.

## Overview

The SlimBus API integration tests have been improved to leverage .NET Aspire host for testing Azure App Configuration scenarios. This provides more realistic testing with proper infrastructure orchestration including Redis cache, SQL Server database, and Azure App Configuration resources.

## Test Structure

### New Aspire-based Tests

**File:** `Integration/AspireAzureAppConfigTests.cs`

This contains enhanced Azure App Configuration integration tests using the .NET Aspire host infrastructure:

- `Application_ShouldStartSuccessfully_WithAspireInfrastructure_WhenAzureAppConfigDisabled`
- `Application_ShouldStartSuccessfully_WithAspireInfrastructure_WhenAzureAppConfigEnabled_WithMockConnectionString`
- `Application_ShouldStartSuccessfully_WithAspireInfrastructure_WhenAzureAppConfigEnabled_WithInvalidConnectionString`
- `Application_ShouldStartSuccessfully_WithAspireInfrastructure_WhenAzureAppConfigEnabled_WithEmptyConnectionString`
- `Application_ShouldServeHealthChecks_WithAspireInfrastructure`

### Updated Existing Tests

**File:** `Integration/AzureAppConfigTests.cs`

The existing tests have been updated to use the `ShareInfraFixture` which provides proper Redis and SQL Server infrastructure, fixing the previous issues with missing `IDistributedCache` dependencies.

## Infrastructure Setup

### AzureAppConfigFixture

**File:** `Fixtures/AzureAppConfigFixture.cs`

This fixture creates a complete Aspire application with:
- Redis cache resource
- SQL Server database resource
- Azure App Configuration resource (for testing)

The fixture provides connection strings for all resources and ensures they are properly started before tests run.

### Dependencies

The following NuGet packages have been added to the test project:

```xml
<PackageReference Include="Aspire.Hosting.Azure.AppConfiguration" Version="9.3.1" />
```

## Running the Tests

### Prerequisites

1. .NET 9.0 SDK
2. Docker (for running Redis and SQL Server containers in Aspire)

### Local Development

To run the tests locally:

```bash
cd Templates/SlimBus.ApiEndpoints
dotnet test SlimBus.App.Tests/SlimBus.App.Tests.csproj --filter "Category=Integration"
```

### CI/CD Pipeline

The tests will run automatically in the CI pipeline. The Aspire infrastructure will be orchestrated within the test execution, so no additional infrastructure setup is required.

## Configuration Options Tested

The tests verify various Azure App Configuration scenarios:

1. **Disabled Configuration**: Tests that the application starts correctly when Azure App Configuration is disabled
2. **Mock Configuration**: Tests with a mock Azure App Configuration connection string
3. **Invalid Configuration**: Tests graceful fallback when an invalid connection string is provided
4. **Empty Configuration**: Tests fallback behavior when no connection string is provided

## Benefits of Aspire Integration

1. **Realistic Testing**: Tests run against actual Redis and SQL Server instances (via containers)
2. **Infrastructure Orchestration**: Aspire handles the setup and teardown of required infrastructure
3. **Improved Reliability**: Tests are more stable with proper dependency management
4. **Better Coverage**: More realistic scenarios are tested, including health checks

## Troubleshooting

### Common Issues

1. **Docker not running**: Ensure Docker is running for container-based resources
2. **Port conflicts**: Aspire will automatically assign available ports
3. **Timeout issues**: Increase test timeout if infrastructure takes longer to start

### Debug Information

The Aspire infrastructure uses:
- DisableDashboard: true (for test scenarios)
- AllowUnsecuredTransport: true (for development/testing)

Connection strings and resource status can be inspected through the fixture properties during debugging.