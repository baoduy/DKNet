// <copyright file="IdempotencyRelationalStoreRoundTripTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Data.Common;
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.NpgsqlStore.Store;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace AspCore.Idempotency.NpgsqlStore.Tests.Integration;

/// <summary>
///     Pins performance finding P2 against a real PostgreSQL database (not a stand-in): reserving a
///     brand-new key issues a single INSERT instead of a SELECT-then-INSERT, and completing a reservation
///     issues a single UPDATE instead of a SELECT-then-SaveChanges. Counts every SQL command PostgreSQL
///     actually receives via a <see cref="DbCommandInterceptor" /> - the shared
///     <see cref="DKNet.AspCore.Idempotency.Relational.Store.IdempotencyRelationalStore{TContext}" /> code
///     this exercises is the same code the MsSql store runs on top of.
/// </summary>
public sealed class IdempotencyRelationalStoreRoundTripTests : IAsyncLifetime
{
    #region Fields

    private readonly CommandCountInterceptor _interceptor = new();
    private PostgreSqlContainer _container = null!;
    private ServiceProvider _serviceProvider = null!;
    private IdempotencyPostgresStore _store = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        await _store.DisposeAsync();
        await _serviceProvider.DisposeAsync();
        await _container.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine").WithCleanUp(true).Build();
        await _container.StartAsync();

        var connectionString = _container.GetConnectionString()
            .Replace("Database=postgres", $"Database=idem_rt_{Guid.NewGuid():N}", StringComparison.OrdinalIgnoreCase);

        var services = new ServiceCollection();
        services.AddDbContextFactory<IdempotencyDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsAssembly(typeof(IdempotencyNpgsqlSetup).Assembly))
                .AddInterceptors(_interceptor));
        _serviceProvider = services.BuildServiceProvider();

        _store = new IdempotencyPostgresStore(
            _serviceProvider, NullLogger<IdempotencyPostgresStore>.Instance, Options.Create(new IdempotencyOptions()));

        // Warm-up call on a throwaway key: the very first call also runs the migration-guard probe
        // (GetPendingMigrationsAsync/MigrateAsync), which would otherwise pollute the round-trip count.
        await _store.IsKeyProcessedAsync(new IdempotentKeyInfo
            { Endpoint = "/api/warmup", Method = "POST", IdempotentKey = Guid.NewGuid().ToString() });
        _interceptor.Count = 0;
    }

    [Fact]
    public async Task IsKeyProcessedAsync_NewKey_ReservesInOneRoundTrip()
    {
        // Act - reserving a brand-new key issues a single INSERT, not a SELECT followed by an INSERT.
        var reserved = await _store.IsKeyProcessedAsync(new IdempotentKeyInfo
            { Endpoint = "/api/orders", Method = "POST", IdempotentKey = Guid.NewGuid().ToString() });

        // Assert
        reserved.processed.ShouldBeFalse();
        _interceptor.Count.ShouldBe(1);
    }

    [Fact]
    public async Task MarkKeyAsProcessedAsync_CompletingAReservation_IssuesOneRoundTrip()
    {
        // Arrange - reserve first (not counted), then reset the counter before completing it.
        var keyInfo = new IdempotentKeyInfo
            { Endpoint = "/api/orders", Method = "POST", IdempotentKey = Guid.NewGuid().ToString() };
        await _store.IsKeyProcessedAsync(keyInfo);
        _interceptor.Count = 0;

        // Act - completing the reservation issues a single UPDATE, not a SELECT followed by an UPDATE.
        await _store.MarkKeyAsProcessedAsync(keyInfo, new CachedResponse
        {
            StatusCode = 201,
            Body = "{\"id\":1}",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        });

        // Assert
        _interceptor.Count.ShouldBe(1);
    }

    #endregion
}

/// <summary>
///     Counts every SQL command EF Core sends to the database - used to pin down the store's round-trip
///     count for a given call (performance finding P2: reserve-by-insert-first, complete via a single
///     UPDATE).
/// </summary>
internal sealed class CommandCountInterceptor : DbCommandInterceptor
{
    public int Count;

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Count++;
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Count++;
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }
}
