// <copyright file="ApiFixture.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;

namespace AspCore.Idempotency.MsSqlStore.Tests.Fixtures;

/// <summary>
///     Web application factory for testing idempotency endpoints with real SQL Server.
///     Provides a minimal web host configured with idempotency services, TestContainers.MsSql,
///     and test endpoints for integration testing.
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<ApiTests.Program>, IAsyncLifetime
{
    #region Fields

    private MsSqlContainer? _container;

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the SQL Server connection string.
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

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
    ///     Disposes the test application and SQL Server container.
    /// </summary>
    public new async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    internal IdempotencyDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
    }

    /// <summary>
    ///     Initializes the test application with TestContainers.MsSql.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Create and start SQL Server container
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("DKNetTest@123!")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Create the HTTP client
        HttpClient ??= CreateClient();
    }

    #endregion
}