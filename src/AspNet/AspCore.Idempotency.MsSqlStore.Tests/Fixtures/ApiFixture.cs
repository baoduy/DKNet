// <copyright file="ApiFixture.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AspCore.Idempotency.MsSqlStore.Tests.Fixtures;

/// <summary>
///     Web application factory for testing idempotency endpoints with real SQL Server.
///     Provides a minimal web host configured with idempotency services, TestContainers.MsSql,
///     and test endpoints for integration testing.
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<ApiTests.Program>, IAsyncLifetime
{
    #region Fields

    private readonly string _databaseName = $"Idem_{Guid.NewGuid():N}";

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the SQL Server connection string.
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    internal string DatabaseName => _databaseName;

    /// <summary>
    ///     Gets the HTTP client for making requests to the test application.
    /// </summary>
    public HttpClient? HttpClient { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    ///     Configures the web host builder for testing with SQL Server and idempotency services.
    /// </summary>
    /// <param name="builder">The web host builder.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureServices(services =>
        {
            // Register idempotency with MS SQL store using the TestContainers connection string
            services
                .AddIdempotencyWithMsSqlStore(
                    ConnectionString,
                    options =>
                    {
                        options.ConflictHandling = IdempotentConflictHandling.CachedResult;
                        options.Expiration = TimeSpan.FromMinutes(2);
                    });
        });
    }

    /// <summary>
    ///     Disposes the test application. No SQL Server container is ever created (see <see cref="InitializeAsync" />).
    /// </summary>
    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }

    internal IdempotencyDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
    }

    /// <summary>
    ///     No-op: the SQL Server idempotency store's tests are retired (see DRK-118), and this fixture must
    ///     guarantee no container is ever started — marking the tests Skip alone does not stop xUnit from
    ///     constructing this <see cref="IClassFixture{TFixture}" />/<see cref="ICollectionFixture{TFixture}" />
    ///     instance and running <see cref="InitializeAsync" /> regardless.
    /// </summary>
    public Task InitializeAsync() => Task.CompletedTask;

    #endregion
}