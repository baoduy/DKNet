// <copyright file="IdempotencyKeyCapTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using AspCore.Idempotency.ApiTests;
using AspCore.Idempotency.MsSqlStore.Tests.Fixtures;
using DKNet.AspCore.Idempotency;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Idempotency.MsSqlStore.Tests.Integration;

/// <summary>
///     DRK-2221 acceptance tests, SQL Server rows: the 500-character key cap holds in this store, the longest
///     allowed key is stored without a database error, and the upgrade deletes stored keys over 500 characters
///     before it narrows the <c>IdempotentKey</c> column to <c>varchar(500)</c>.
/// </summary>
[Collection("Api Collection")]
public sealed class IdempotencyKeyCapTests(ApiFixture fixture)
{
    #region Fields

    private const string UnboundIdempotentKeyMigration = "20261010121749_UnboundIdempotentKey";

    #endregion

    #region Methods

    [Fact]
    [Trait("Scenario", "The 500 cap holds in every store")]
    public async Task CreateOrder_SqlServerStoreWithLimit1000AndKeyOf501_Returns400AndStoresNothing()
    {
        // Arrange
        await using var app = AppWithKeyLimit(1000);
        using var client = app.CreateClient();
        var key = RandomKey(501);

        // Act
        var response = await CreateOrderAsync(client, key);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await StoredKeysAsync(key)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Scenario", "The longest allowed key is stored without a database error")]
    public async Task CreateOrder_SqlServerStoreWithLimit500AndKeyOf500_IsCreatedStoredAndReplayed()
    {
        // Arrange
        await using var app = AppWithKeyLimit(500);
        using var client = app.CreateClient();
        var key = RandomKey(500);

        // Act
        var first = await CreateOrderAsync(client, key);
        var retry = await CreateOrderAsync(client, key);

        // Assert - the body carries a fresh Guid per handler run, so an identical body proves a replay
        var firstBody = await first.Content.ReadAsStringAsync();
        first.StatusCode.ShouldBe(HttpStatusCode.Created, firstBody);
        retry.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await retry.Content.ReadAsStringAsync()).ShouldBe(firstBody);
        (await StoredKeysAsync(key)).ShouldBe([key]);
    }

    [Fact]
    [Trait("Scenario", "The upgrade removes stored keys over 500 and keeps the rest")]
    public async Task Upgrade_StoredKeysOf40_500_501And600Chars_DeletesKeysOver500AndNarrowsColumnTo500()
    {
        // Arrange - a database of its own, on the schema shipped before this release, holding the four keys
        await using var scope = await NewDatabaseAtUnboundSchemaAsync();
        var dbContext = scope.DbContext;
        var key40 = RandomKey(40);
        var key500 = RandomKey(500);
        var key501 = RandomKey(501);
        var key600 = RandomKey(600);
        foreach (var key in new[] { key40, key500, key501, key600 })
            await InsertRawKeyAsync(dbContext, key);
        (await AllStoredKeysAsync(dbContext)).OrderBy(k => k.Length).ShouldBe([key40, key500, key501, key600]);

        // Act
        await dbContext.GetService<IMigrator>().MigrateAsync();

        // Assert
        (await AllStoredKeysAsync(dbContext)).OrderBy(k => k.Length).ShouldBe([key40, key500]);
        (await ColumnTypeAsync(dbContext)).ShouldBe("varchar:500:NO");
        (await dbContext.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Scenario", "The upgrade removes stored keys over 500 and keeps the rest")]
    public async Task Upgrade_EmptyTable_SucceedsAndNarrowsColumnTo500()
    {
        // Arrange
        await using var scope = await NewDatabaseAtUnboundSchemaAsync();
        var dbContext = scope.DbContext;
        (await AllStoredKeysAsync(dbContext)).ShouldBeEmpty();

        // Act
        await dbContext.GetService<IMigrator>().MigrateAsync();

        // Assert
        (await ColumnTypeAsync(dbContext)).ShouldBe("varchar:500:NO");
        (await dbContext.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
    }

    /// <summary>
    ///     A host on the shared fixture's database with the key limit overridden for this test only.
    /// </summary>
    private WebApplicationFactory<ApiTests.Program> AppWithKeyLimit(int limit) =>
        fixture.WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.PostConfigure<IdempotencyOptions>(o => o.MaxIdempotencyKeyLength = limit)));

    private async Task<MigrationScope> NewDatabaseAtUnboundSchemaAsync()
    {
        var services = new ServiceCollection();
        services.AddIdempotencyMsSqlStore(
            fixture.ConnectionString.Replace(fixture.DatabaseName, $"Idem_Cap_{Guid.NewGuid():N}", StringComparison.Ordinal));
        var provider = services.BuildServiceProvider();
        var dbContext = provider.GetRequiredService<IdempotencyDbContext>();
        await dbContext.GetService<IMigrator>().MigrateAsync(UnboundIdempotentKeyMigration);
        (await ColumnTypeAsync(dbContext)).ShouldBe("varchar:-1:NO");
        return new MigrationScope(provider, dbContext);
    }

    // Raw SQL, not the entity: the narrowed model cannot write a key the old column still holds.
    private static Task InsertRawKeyAsync(IdempotencyDbContext dbContext, string key) =>
        dbContext.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO [IdempotencyKeys] ([Id], [IdempotentKey], [Endpoint], [Method], [CompositeKey], [StatusCode], [ContentType], [CreatedAt])
             VALUES ({Guid.NewGuid()}, {key}, '/API/ORDERS', 'POST', {Guid.NewGuid().ToString("N")}, 201, 'application/json', SYSDATETIMEOFFSET())
             """);

    private async Task<List<string>> StoredKeysAsync(string key)
    {
        await using var dbContext = fixture.GetDbContext();
        return await dbContext.IdempotencyKeys.AsNoTracking()
            .Where(k => k.IdempotentKey == key)
            .Select(k => k.IdempotentKey)
            .ToListAsync();
    }

    private static async Task<List<string>> AllStoredKeysAsync(IdempotencyDbContext dbContext) =>
        await dbContext.Database.SqlQueryRaw<string>("SELECT [IdempotentKey] AS Value FROM [IdempotencyKeys]")
            .ToListAsync();

    private static async Task<string> ColumnTypeAsync(IdempotencyDbContext dbContext) =>
        await dbContext.Database.SqlQueryRaw<string>(
                "SELECT DATA_TYPE + ':' + CAST(CHARACTER_MAXIMUM_LENGTH AS varchar(10)) + ':' + IS_NULLABLE AS Value " +
                "FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'IdempotencyKeys' AND COLUMN_NAME = 'IdempotentKey'")
            .SingleAsync();

    private static Task<HttpResponseMessage> CreateOrderAsync(HttpClient client, string key) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Headers = { { "X-Idempotency-Key", key } },
            Content = JsonContent.Create(new CreateItemRequest { Name = "checkout-service order" })
        });

    // Letters only, fresh per call, so no earlier case can replay a cached response.
    private static string RandomKey(int length) =>
        new(Enumerable.Range(0, length).Select(_ => (char)('a' + Random.Shared.Next(26))).ToArray());

    #endregion

    #region Nested Types

    private sealed record MigrationScope(ServiceProvider Provider, IdempotencyDbContext DbContext) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Provider.DisposeAsync();
        }
    }

    #endregion
}
