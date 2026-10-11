// <copyright file="IdempotencyUnboundKeyStoreTests.cs" company="https://drunkcoding.net">
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
///     DRK-2214, capped by DRK-2221: the <c>IdempotentKey</c> column stores the longest key the 500-character
///     cap allows, and migrating down to the shipped Initial schema and back up to the 500-wide column keeps
///     existing rows.
/// </summary>
[Collection("Api Collection")]
public sealed class IdempotencyUnboundKeyStoreTests(ApiFixture fixture)
{
    #region Methods

    [Fact]
    public async Task IsKeyProcessedAsync_KeyOf500Chars_StoresFullKeyAndReplaysResponse()
    {
        // Arrange - the longest key the 500-character cap allows, as a consumer raising the option would send
        var store = fixture.Services.GetRequiredService<IIdempotencyKeyStore>();
        var keyInfo = new IdempotentKeyInfo { Endpoint = "/API/ITEMS", Method = "POST", IdempotentKey = RandomKey(500) };

        // Act
        var reserved = await store.IsKeyProcessedAsync(keyInfo);
        await store.MarkKeyAsProcessedAsync(keyInfo, new CachedResponse
        {
            StatusCode = 201,
            Body = "{\"id\":1}",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        });
        var replayed = await store.IsKeyProcessedAsync(keyInfo);

        // Assert
        reserved.processed.ShouldBeFalse();
        replayed.processed.ShouldBeTrue();
        replayed.response!.Body.ShouldBe("{\"id\":1}");

        await using var dbContext = fixture.GetDbContext();
        var storedKeys = await dbContext.IdempotencyKeys
            .Where(k => k.IdempotentKey == keyInfo.IdempotentKey)
            .Select(k => k.IdempotentKey)
            .ToListAsync();
        storedKeys.ShouldBe([keyInfo.IdempotentKey]);
    }

    [Fact]
    public async Task UnboundIdempotentKeyMigration_DownThenUp_KeepsExistingKeyAndTogglesColumnTypeTo500()
    {
        // Arrange - an empty database of its own on the fixture's server, migrated to the shipped Initial schema
        var services = new ServiceCollection();
        services.AddIdempotencyNpgsqlStore(
            fixture.ConnectionString.Replace(fixture.DatabaseName, $"Idem_Mig_{Guid.NewGuid():N}", StringComparison.Ordinal));
        await using var provider = services.BuildServiceProvider();
        await using var dbContext = provider.GetRequiredService<IdempotencyDbContext>();
        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync("20260804081005_Initial");
        (await ColumnTypeAsync(dbContext)).ShouldBe("character varying:150:NO");

        var existingKey = RandomKey(150);
        dbContext.IdempotencyKeys.Add(new IdempotencyKeyEntity(
            new IdempotentKeyInfo { Endpoint = "/API/ITEMS", Method = "POST", IdempotentKey = existingKey },
            new CachedResponse
            {
                StatusCode = 201, Body = null, ContentType = "application/json",
                CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = null
            }));
        await dbContext.SaveChangesAsync();

        // Act & Assert - Up to the latest schema leaves the column 500 wide and keeps the row
        await migrator.MigrateAsync();
        (await ColumnTypeAsync(dbContext)).ShouldBe("character varying:500:NO");
        (await StoredKeysAsync(dbContext)).ShouldBe([existingKey]);

        // Down restores the shipped width and keeps the row
        await migrator.MigrateAsync("20260804081005_Initial");
        (await ColumnTypeAsync(dbContext)).ShouldBe("character varying:150:NO");
        (await StoredKeysAsync(dbContext)).ShouldBe([existingKey]);

        // Up again leaves nothing pending
        await migrator.MigrateAsync();
        (await ColumnTypeAsync(dbContext)).ShouldBe("character varying:500:NO");
        (await dbContext.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
    }

    private static async Task<string> ColumnTypeAsync(IdempotencyDbContext dbContext) =>
        await dbContext.Database.SqlQueryRaw<string>(
                "SELECT data_type || ':' || COALESCE(character_maximum_length::text, '') || ':' || is_nullable AS \"Value\" " +
                "FROM information_schema.columns WHERE table_name = 'IdempotencyKeys' AND column_name = 'IdempotentKey'")
            .SingleAsync();

    private static async Task<List<string>> StoredKeysAsync(IdempotencyDbContext dbContext) =>
        await dbContext.IdempotencyKeys.AsNoTracking().Select(k => k.IdempotentKey).ToListAsync();

    private static string RandomKey(int length) =>
        new(Enumerable.Range(0, length).Select(_ => (char)('a' + Random.Shared.Next(26))).ToArray());

    #endregion
}
