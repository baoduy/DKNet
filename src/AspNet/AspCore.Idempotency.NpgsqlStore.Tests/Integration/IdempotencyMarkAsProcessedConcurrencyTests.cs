// <copyright file="IdempotencyMarkAsProcessedConcurrencyTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace AspCore.Idempotency.NpgsqlStore.Tests.Integration;

/// <summary>
///     Covers <see cref="IdempotencyRelationalStore{TContext}.MarkKeyAsProcessedAsync" />'s defensive
///     fallback: calling it directly without a prior <c>IsKeyProcessedAsync</c> reservation (e.g. a
///     retried background job) is unusual but must never surface a raw unique-constraint exception if a
///     second caller races the same key through the same fallback path.
/// </summary>
public sealed class IdempotencyMarkAsProcessedConcurrencyTests : IAsyncLifetime
{
    #region Fields

    private PostgreSqlContainer _container = null!;
    private ServiceProvider _serviceProvider = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _container.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine").WithCleanUp(true).Build();
        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdempotencyWithNpgsqlStore(_container.GetConnectionString()
            .Replace("Database=postgres", $"Database=idem_mark_{Guid.NewGuid():N}", StringComparison.OrdinalIgnoreCase));
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task MarkKeyAsProcessedAsync_ConcurrentCallsWithoutPriorReservation_SwallowsUniqueViolationDefensively()
    {
        // Arrange - a key nobody has reserved via IsKeyProcessedAsync, so every concurrent call below
        // takes the defensive "entity is null -> Add" fallback rather than the normal Complete() path.
        var store = _serviceProvider.GetRequiredService<IIdempotencyKeyStore>();
        var keyInfo = new IdempotentKeyInfo
        {
            Endpoint = "/api/jobs", Method = "POST", IdempotentKey = Guid.NewGuid().ToString()
        };
        var response = new CachedResponse
        {
            StatusCode = 200,
            Body = "{}",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        // Act - fire several concurrent calls for the same key; at least two race past the "entity is
        // null" check before either commits, so one's INSERT collides with the other's.
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => store.MarkKeyAsProcessedAsync(keyInfo, response).AsTask())
            .ToArray();

        // Assert - none of the calls throws (the unique-violation catch in MarkKeyAsProcessedAsync
        // swallows the collision instead of letting DbUpdateException propagate).
        await Task.WhenAll(tasks);

        // Assert - exactly one row exists for the key despite the concurrent inserts: the unique index
        // still allowed only one Add() to succeed, and every other caller's collision was swallowed.
        var factory = _serviceProvider.GetRequiredService<IDbContextFactory<IdempotencyDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var count = await dbContext.IdempotencyKeys
            .CountAsync(k => k.IdempotentKey == keyInfo.IdempotentKey);
        count.ShouldBe(1);
    }

    #endregion
}
