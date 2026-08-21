// <copyright file="ApiFixture.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Runtime.InteropServices;
using DotNet.Testcontainers.Builders;
using DKNet.AspCore.Idempotency;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
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

    // mssql/server ships x64-only images (no ARM64), so on ARM hosts fall back to azure-sql-edge
    // which has a native ARM64 build. x64 (incl. GitHub-hosted runners) keeps real SQL Server.
    //
    // NOTE: azure-sql-edge runs on Apple Silicon Macs, but NOT on every ARM64 device (e.g. GX10).
    // Where it can't run, this local fallback is useless - verify via the remote-tests GitHub
    // Action (gh workflow run remote-tests.yml --ref <branch>), which runs on an x64 runner. A
    // green run here is never proof the tests pass on real SQL Server; that gate is the x64 runner.
    private static readonly string MssqlImage =
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "mcr.microsoft.com/azure-sql-edge:latest"
            : "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly string _databaseName = $"Idem_{Guid.NewGuid():N}";
    private MsSqlContainer? _container;

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
        // Create and start the SQL Server container.
        _container = new MsSqlBuilder(MssqlImage)
            .WithPassword($"A{Guid.NewGuid():N}a!")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("SQL Server is now ready for client connections"))
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();

        // Testcontainers.MsSql's connection string targets the container's "master" database.
        // Unlike SQLite, SQL Server does not create a database implicitly on first connection,
        // so the per-test database must be provisioned before the app under test connects to it.
        //
        // ponytail: mssql/server can log "ready for client connections" a moment before the sa
        // password is actually applied, so the very first login can transiently fail with
        // "Login failed for user 'sa'" under CPU contention - retry instead of widening the wait
        // strategy. Ceiling: 5 attempts / 2s apart; raise if it still isn't enough headroom.
        // Only the login (OpenAsync) is retried - a CREATE DATABASE failure after a successful
        // login is a real error and must surface immediately, not get masked by a retry that then
        // fails with "database already exists".
        var masterConnectionString = _container.GetConnectionString();
        SqlConnection masterConnection;
        for (var attempt = 1; ; attempt++)
        {
            masterConnection = new SqlConnection(masterConnectionString);
            try
            {
                await masterConnection.OpenAsync();
                break;
            }
            catch (SqlException) when (attempt < 5)
            {
                await masterConnection.DisposeAsync();
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        await using (masterConnection)
        {
            await using var createDatabaseCommand = masterConnection.CreateCommand();
            createDatabaseCommand.CommandText = $"CREATE DATABASE [{_databaseName}]";
            await createDatabaseCommand.ExecuteNonQueryAsync();
        }

        ConnectionString = _container.GetConnectionString()
            .Replace("Database=master", $"Database={_databaseName}", StringComparison.OrdinalIgnoreCase);

        // Create the HTTP client
        HttpClient ??= CreateClient();
    }

    #endregion
}