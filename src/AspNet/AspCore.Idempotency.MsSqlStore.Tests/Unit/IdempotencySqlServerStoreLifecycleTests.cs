// <copyright file="IdempotencySqlServerStoreLifecycleTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.MsSqlStore.Store;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AspCore.Idempotency.MsSqlStore.Tests.Unit;

/// <summary>
///     Covers the reserve/complete lifecycle and the collision-handler branches of
///     <see cref="IdempotencySqlServerStore" /> that <see cref="IdempotencySqlServerStoreConcurrencyTests" />
///     doesn't exercise. Uses the same file-based SQLite setup so it runs without Docker/SQL Server.
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
                    typeof(IdempotencySqlServerStoreConcurrencyTests).Assembly.GetName().Name))
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

    [Fact]
    public async Task IsKeyProcessedAsync_FullLifecycle_ReserveThenCompleteReturnsCachedResponse()
    {
        // Arrange
        var keyInfo = new IdempotentKeyInfo
        {
            Endpoint = "/api/orders",
            Method = "POST",
            IdempotentKey = Guid.NewGuid().ToString()
        };

        // Act - first call finds nothing and reserves the key
        var reserved = await _store.IsKeyProcessedAsync(keyInfo);

        var response = CreateResponse(201, "{\"id\":1}", DateTimeOffset.UtcNow.AddHours(1));
        await _store.MarkKeyAsProcessedAsync(keyInfo, response);

        var completed = await _store.IsKeyProcessedAsync(keyInfo);

        // Assert - reservation reports not-yet-processed, then the completed row replays the cached response
        reserved.processed.ShouldBeFalse();
        reserved.response.ShouldBeNull();

        completed.processed.ShouldBeTrue();
        completed.response.ShouldNotBeNull();
        completed.response!.StatusCode.ShouldBe(201);
        completed.response.Body.ShouldBe("{\"id\":1}");
    }

    [Fact]
    public async Task IsKeyProcessedAsync_WhileReservationInFlight_ReturnsTrueWithNullResponse()
    {
        // Arrange
        var keyInfo = new IdempotentKeyInfo
        {
            Endpoint = "/api/orders",
            Method = "POST",
            IdempotentKey = Guid.NewGuid().ToString()
        };

        // Act - reserve, then re-check before anyone completes it
        await _store.IsKeyProcessedAsync(keyInfo);
        var inFlight = await _store.IsKeyProcessedAsync(keyInfo);

        // Assert - the caller is told the key is already being processed (409 path), with no cached body yet
        inFlight.processed.ShouldBeTrue();
        inFlight.response.ShouldBeNull();
    }

    [Fact]
    public async Task IsKeyProcessedAsync_CollisionOnInsert_ReQueryReturnsCompletedResponse()
    {
        // Arrange - seed a completed row directly. ExpiresAt is left null so the initial
        // "ExpiresAt > now" lookup in IsKeyProcessedAsync misses it (null compares false), forcing the
        // store down the reserve-insert path, which then collides with this row's unique CompositeKey.
        var keyInfo = new IdempotentKeyInfo
        {
            Endpoint = "/api/orders",
            Method = "POST",
            IdempotentKey = Guid.NewGuid().ToString()
        };
        var response = CreateResponse(200, "{\"id\":42}", null);

        var factory = _serviceProvider.GetRequiredService<IDbContextFactory<IdempotencyDbContext>>();
        await using (var seedContext = await factory.CreateDbContextAsync())
        {
            seedContext.IdempotencyKeys.Add(new IdempotencyKeyEntity(keyInfo, response));
            await seedContext.SaveChangesAsync();
        }

        // Act
        var result = await _store.IsKeyProcessedAsync(keyInfo);

        // Assert - the collision handler re-queries and returns the already-completed response
        result.processed.ShouldBeTrue();
        result.response.ShouldNotBeNull();
        result.response!.StatusCode.ShouldBe(200);
        result.response.Body.ShouldBe("{\"id\":42}");
    }

    [Fact]
    public async Task IsKeyProcessedAsync_ExpiredReservationCollision_ReturnsFalseForFreshReservation()
    {
        // Arrange - a very short InFlightReservationTimeout so the reservation row is expired by the
        // time the second call collides with it, without actually waiting out the 30s default.
        var shortTimeoutStore = CreateStore(new IdempotencyOptions
        {
            InFlightReservationTimeout = TimeSpan.FromMilliseconds(1)
        });
        var keyInfo = new IdempotentKeyInfo
        {
            Endpoint = "/api/orders",
            Method = "POST",
            IdempotentKey = Guid.NewGuid().ToString()
        };

        // Act
        await shortTimeoutStore.IsKeyProcessedAsync(keyInfo); // reserves, expiring almost immediately
        await Task.Delay(50);
        var result = await shortTimeoutStore.IsKeyProcessedAsync(keyInfo); // collides, re-query finds it expired

        // Assert - Rule R1: an abandoned/expired reservation must not permanently block retries
        result.processed.ShouldBeFalse();
        result.response.ShouldBeNull();

        await shortTimeoutStore.DisposeAsync();
    }

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
        var result = await _store.IsKeyProcessedAsync(keyInfo);

        // Assert
        result.processed.ShouldBeTrue();
        result.response.ShouldNotBeNull();
        result.response!.StatusCode.ShouldBe(204);
    }

    #endregion
}
