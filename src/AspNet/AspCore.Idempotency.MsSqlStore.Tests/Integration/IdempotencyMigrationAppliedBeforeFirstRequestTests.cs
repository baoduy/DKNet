// <copyright file="IdempotencyMigrationAppliedBeforeFirstRequestTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Runtime.InteropServices;
using System.Threading;
using DKNet.AspCore.Idempotency;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace AspCore.Idempotency.MsSqlStore.Tests.Integration;

/// <summary>
///     DRK-1507 §5, Scenario "Pending schema changes are applied once, before the first request" (§3 row 6).
///     Runs against its own SQL Server container - not the shared <c>ApiFixture</c>/<c>ApiCollection</c>, which
///     forces the host to start (and so the migration to run) the moment its <c>InitializeAsync</c> completes -
///     so this test can observe the database both before and after an explicit, controlled host start.
///     <para>
///         ARM64 note: like <c>Fixtures/ApiFixture.cs</c>, this falls back to <c>azure-sql-edge</c> on ARM64,
///         which works on Apple Silicon but not on every ARM64 host (see <c>CLAUDE.md</c>). Excluded from this
///         run's local verification for that reason; re-validate via
///         <c>gh workflow run remote-tests.yml --ref &lt;branch&gt;</c> on an x64 runner.
///     </para>
/// </summary>
public sealed class IdempotencyMigrationAppliedBeforeFirstRequestTests : IAsyncLifetime
{
    #region Fields

    private static readonly string MssqlImage =
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "mcr.microsoft.com/azure-sql-edge:latest"
            : "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly string _databaseName = $"Idem_{Guid.NewGuid():N}";
    private MsSqlContainer? _container;
    private string _connectionString = string.Empty;

    #endregion

    #region Test support

    /// <summary>
    ///     The smallest seam this scenario needs (brief §9 Q2): a request counter incremented by terminal
    ///     middleware, so "the API has not yet served a request" is observable without a real endpoint call.
    /// </summary>
    private sealed class RequestCounter
    {
        private int _count;

        public int Count => _count;

        public void Increment() => Interlocked.Increment(ref _count);
    }

    private async Task<bool> TableExistsAsync(string tableName)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @tableName";
        command.Parameters.AddWithValue("@tableName", tableName);
        return (int)(await command.ExecuteScalarAsync())! > 0;
    }

    #endregion

    #region Methods

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder(MssqlImage)
            .WithPassword($"A{Guid.NewGuid():N}a!")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("SQL Server is now ready for client connections"))
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();

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

        _connectionString = _container.GetConnectionString()
            .Replace("Database=master", $"Database={_databaseName}", StringComparison.OrdinalIgnoreCase);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task Migration_HostStarts_AppliesSchemaOnceBeforeFirstRequestIsServed()
    {
        // Arrange - the idempotency table must not exist yet
        (await TableExistsAsync("IdempotencyKeys")).ShouldBeFalse();

        var requestCounter = new RequestCounter();
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(requestCounter);
        builder.Services.AddIdempotencyWithMsSqlStore(_connectionString);

        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            requestCounter.Increment();
            await next(context);
        });
        app.MapGet("/api/health", () => Results.Ok());

        // Act - start the host explicitly (never app.RunAsync(), which would also block)
        await app.StartAsync();

        // Assert - schema applied, exactly once, before any request reached the middleware
        (await TableExistsAsync("IdempotencyKeys")).ShouldBeTrue();

        await using (var dbContext = app.Services.GetRequiredService<IDbContextFactory<IdempotencyDbContext>>()
                         .CreateDbContext())
        {
            (await dbContext.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        }

        requestCounter.Count.ShouldBe(0);

        await app.StopAsync();
    }

    #endregion
}
