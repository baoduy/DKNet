// <copyright file="IdempotencySqlServerStoreLifecycleTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.MsSqlStore.Store;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AspCore.Idempotency.MsSqlStore.Tests.Unit;

/// <summary>
///     Covers the part of <see cref="IdempotencySqlServerStore" />'s lifecycle still reachable without a real
///     SQL Server — see the removal note below for why that is now only the defensive
///     <c>MarkKeyAsProcessedAsync</c> branch. Uses a file-based SQLite setup so this class runs without
///     Docker/SQL Server.
/// </summary>
public sealed class IdempotencySqlServerStoreLifecycleTests : IAsyncLifetime
{
    #region Fields

    private readonly string _dbFilePath =
        Path.Combine(Path.GetTempPath(), $"idempotency-lifecycle-{Guid.NewGuid():N}.db");

    private ServiceProvider _serviceProvider = null!;
    private IdempotencySqlServerStore _store = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        await _store.DisposeAsync();
        await _serviceProvider.DisposeAsync();
        File.Delete(_dbFilePath);
    }

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<IdempotencyDbContext>(o => o.UseSqlite(
                $"Data Source={_dbFilePath}",
                sqlite => sqlite.MigrationsAssembly(
                    typeof(IdempotencySqlServerStoreLifecycleTests).Assembly.GetName().Name))
            .ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>());
        _serviceProvider = services.BuildServiceProvider();

        var factory = _serviceProvider.GetRequiredService<IDbContextFactory<IdempotencyDbContext>>();
        await using var setupContext = await factory.CreateDbContextAsync();
        await setupContext.Database.EnsureCreatedAsync();

        _store = CreateStore(new IdempotencyOptions());
    }

    private IdempotencySqlServerStore CreateStore(IdempotencyOptions options) =>
        new(_serviceProvider, Options.Create(options), NullLogger<IdempotencySqlServerStore>.Instance);

    private static CachedResponse CreateResponse(int statusCode, string? body, DateTimeOffset? expiresAt) =>
        new()
        {
            StatusCode = statusCode,
            Body = body,
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        };

    // IsKeyProcessedAsync_CollisionOnInsert_ReQueryReturnsCompletedResponse and
    // IsKeyProcessedAsync_ExpiredReservationCollision_ReturnsFalseForFreshReservation used to live here,
    // simulating a unique-constraint collision by relying on SQLite's incidental "UNIQUE" message text.
    // Now that IsUniqueViolation (DRK-324/DRK-355) checks SqlException.Number instead, SQLite can no
    // longer trigger that catch - it was always an implementation detail of the old check, not a contract.
    //
    // IsKeyProcessedAsync_FullLifecycle_ReserveThenCompleteReturnsCachedResponse and
    // IsKeyProcessedAsync_WhileReservationInFlight_ReturnsTrueWithNullResponse followed them for the same
    // reason once ReserveKeyAsync started reserving by INSERT-first instead of SELECT-then-INSERT: every
    // repeat call on a key now routes through that same unique-violation catch, so on SQLite the
    // DbUpdateException escapes instead of being classified. Both are covered end to end against real SQL
    // Server by the Testcontainer-backed AspCore.Idempotency.MsSqlStore.Tests.Integration
    // .IdempotencyIntegrationTests - CreateItem_WithSameIdempotencyKey_SecondRequest_ReturnsCachedResponse
    // (cached replay) and CreateItem_ConcurrentRequestsWithSameKey_OnlyOneProcessed (in-flight conflict,
    // DRK-118, un-skipped by DRK-362). This matches IdempotencyPostgresStore's own suite, which likewise
    // keeps no SQLite stand-in for the collision path.

    [Fact]
    public async Task MarkKeyAsProcessedAsync_WithoutPriorReservation_CreatesEntityDefensively()
    {
        // Arrange - call MarkKeyAsProcessedAsync directly, skipping IsKeyProcessedAsync's reservation.
        // Exercises the defensive "entity is null" branch that should not happen in the normal flow.
        var keyInfo = new IdempotentKeyInfo
        {
            Endpoint = "/api/orders",
            Method = "POST",
            IdempotentKey = Guid.NewGuid().ToString()
        };
        var response = CreateResponse(204, null, DateTimeOffset.UtcNow.AddHours(1));

        // Act
        await _store.MarkKeyAsProcessedAsync(keyInfo, response);

        // Assert - read the row back directly. Re-checking via IsKeyProcessedAsync would reserve by
        // INSERT and land in the SqlException-only unique-violation catch SQLite cannot satisfy.
        var factory = _serviceProvider.GetRequiredService<IDbContextFactory<IdempotencyDbContext>>();
        await using var assertContext = await factory.CreateDbContextAsync();
        var stored = await assertContext.IdempotencyKeys.AsNoTracking()
            .SingleAsync(k => k.IdempotentKey == keyInfo.IdempotentKey);

        stored.StatusCode.ShouldBe(204);
        stored.Body.ShouldBeNull();
    }

    #endregion
}

/// <summary>
///     Applies <see cref="IdempotencyKeyConfiguration" /> as-is, then drops the one column-type override in it
///     that is SQL-Server-specific raw SQL, so <c>EnsureCreatedAsync</c> can generate valid SQLite DDL.
/// </summary>
internal sealed class SqliteCompatibleModelCustomizer(ModelCustomizerDependencies dependencies)
    : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        var key = modelBuilder.Entity<IdempotencyKeyEntity>();
        key.Property(e => e.Body).HasColumnType(null);

        // The Sqlite provider cannot translate "> "/"<" comparisons on a DateTimeOffset column (only equality) -
        // IsKeyProcessedAsync's expiry check relies on exactly that. Store ExpiresAt as UTC ticks instead so the
        // comparison becomes an ordinary numeric one; the property's C# type/behaviour is unaffected.
        key.Property(e => e.ExpiresAt).HasConversion(
            v => v.HasValue ? v.Value.UtcTicks : (long?)null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null);
    }
}
