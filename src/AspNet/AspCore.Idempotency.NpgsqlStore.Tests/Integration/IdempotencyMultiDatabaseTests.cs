// <copyright file="IdempotencyMultiDatabaseTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace AspCore.Idempotency.NpgsqlStore.Tests.Integration;

/// <summary>
///     Proves the migration guard fixed by DRK-583 (<see cref="IdempotencyRelationalStore{TContext}" />) is
///     keyed per connection string, not a single process-wide flag: a host that registers
///     <see cref="IdempotencyPostgresStore" /> against two separate databases (e.g. per-tenant) must have
///     BOTH prepared, not just the first one it touches. Before the fix, <c>IdempotencyPostgresStore</c>
///     tracked "migrations ensured" with a single static <c>int</c>, so a second database's
///     <c>IdempotencyKeys</c> table was never created and every request against it failed.
/// </summary>
public sealed class IdempotencyMultiDatabaseTests : IAsyncLifetime
{
    #region Fields

    private PostgreSqlContainer _container = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine").WithCleanUp(true).Build();
        await _container.StartAsync();
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdempotencyWithNpgsqlStore(connectionString);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task IsKeyProcessedAsync_TwoNeverSeenDatabasesOnSameProvider_BothGetPreparedAndReserve()
    {
        // Arrange - two brand-new, never-migrated databases on the SAME Postgres instance, each behind
        // its own DI-registered IdempotencyPostgresStore - exactly how a host serving two tenant
        // databases through one provider would wire it up.
        var connectionStringA = _container.GetConnectionString()
            .Replace("Database=postgres", $"Database=idem_a_{Guid.NewGuid():N}", StringComparison.OrdinalIgnoreCase);
        var connectionStringB = _container.GetConnectionString()
            .Replace("Database=postgres", $"Database=idem_b_{Guid.NewGuid():N}", StringComparison.OrdinalIgnoreCase);

        await using var providerA = BuildProvider(connectionStringA);
        await using var providerB = BuildProvider(connectionStringB);

        var storeA = providerA.GetRequiredService<IIdempotencyKeyStore>();
        var storeB = providerB.GetRequiredService<IIdempotencyKeyStore>();

        var keyInfoA = new IdempotentKeyInfo
        {
            Endpoint = "/api/orders", Method = "POST", IdempotentKey = Guid.NewGuid().ToString()
        };
        var keyInfoB = new IdempotentKeyInfo
        {
            Endpoint = "/api/orders", Method = "POST", IdempotentKey = Guid.NewGuid().ToString()
        };

        // Act - touch database A first, letting its store migrate it, THEN touch database B. Under the
        // old process-wide guard, database A's success flagged migrations "ensured" for every
        // IdempotencyPostgresStore instance in the process, so database B's first call would throw
        // instead of migrating it.
        var resultA = await storeA.IsKeyProcessedAsync(keyInfoA);
        var resultB = await storeB.IsKeyProcessedAsync(keyInfoB);

        // Assert - both databases were independently prepared: each reservation succeeded (fresh key,
        // nothing cached yet).
        resultA.processed.ShouldBeFalse();
        resultA.response.ShouldBeNull();
        resultB.processed.ShouldBeFalse();
        resultB.response.ShouldBeNull();
    }

    #endregion
}
