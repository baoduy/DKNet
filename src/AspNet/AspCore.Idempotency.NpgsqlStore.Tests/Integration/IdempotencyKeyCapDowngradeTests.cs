// <copyright file="IdempotencyKeyCapDowngradeTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using AspCore.Idempotency.NpgsqlStore.Tests.Fixtures;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Idempotency.NpgsqlStore.Tests.Integration;

/// <summary>
///     DRK-2221: migrating down from the 500-wide <c>IdempotentKey</c> column restores the unbounded column
///     shipped in <c>UnboundIdempotentKey</c> and keeps the stored keys.
/// </summary>
[Collection("Api Collection")]
public sealed class IdempotencyKeyCapDowngradeTests(ApiFixture fixture)
{
    #region Methods

    [Fact]
    public async Task CapIdempotentKeyAt500Migration_Down_RestoresUnboundColumnAndKeepsStoredKey()
    {
        // Arrange - a database of its own at the latest schema, holding the longest key the cap allows
        var services = new ServiceCollection();
        services.AddIdempotencyNpgsqlStore(
            fixture.ConnectionString.Replace(fixture.DatabaseName, $"Idem_Down_{Guid.NewGuid():N}", StringComparison.Ordinal));
        await using var provider = services.BuildServiceProvider();
        await using var dbContext = provider.GetRequiredService<IdempotencyDbContext>();
        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync();
        var key = new string('k', 500);
        dbContext.IdempotencyKeys.Add(new IdempotencyKeyEntity(
            new IdempotentKeyInfo { Endpoint = "/API/ITEMS", Method = "POST", IdempotentKey = key },
            new CachedResponse
            {
                StatusCode = 201, Body = null, ContentType = "application/json",
                CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = null
            }));
        await dbContext.SaveChangesAsync();
        (await ColumnTypeAsync(dbContext)).ShouldBe("character varying:500:NO");

        // Act
        await migrator.MigrateAsync("20261010121750_UnboundIdempotentKey");

        // Assert
        (await ColumnTypeAsync(dbContext)).ShouldBe("text::NO");
        (await dbContext.IdempotencyKeys.AsNoTracking().Select(k => k.IdempotentKey).ToListAsync()).ShouldBe([key]);
    }

    private static async Task<string> ColumnTypeAsync(IdempotencyDbContext dbContext) =>
        await dbContext.Database.SqlQueryRaw<string>(
                "SELECT data_type || ':' || COALESCE(character_maximum_length::text, '') || ':' || is_nullable AS \"Value\" " +
                "FROM information_schema.columns WHERE table_name = 'IdempotencyKeys' AND column_name = 'IdempotentKey'")
            .SingleAsync();

    #endregion
}
