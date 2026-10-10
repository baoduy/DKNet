// <copyright file="IdempotencyKeyLengthTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using AspCore.Idempotency.ApiTests;
using AspCore.Idempotency.NpgsqlStore.Tests.Fixtures;

namespace AspCore.Idempotency.NpgsqlStore.Tests.Integration;

/// <summary>
///     DRK-2214: every idempotency key the filter accepts (up to the default
///     <c>MaxIdempotencyKeyLength</c> of 255) must reach the route handler and be stored unchanged.
/// </summary>
[Collection("Api Collection")]
public sealed class IdempotencyKeyLengthTests(ApiFixture fixture)
{
    #region Methods

    [Theory]
    [InlineData(1)]
    [InlineData(150)]
    public async Task CreateItem_KeyWithinOldColumnWidth_ReachesHandlerAndStoresFullKey(int length) =>
        await AssertKeyReachesHandlerAndIsStoredAsync(length);

    [Theory]
    [InlineData(151)]
    [InlineData(200)]
    [InlineData(255)]
    public async Task CreateItem_KeyLongerThanOldColumnWidth_ReachesHandlerAndStoresFullKey(int length) =>
        await AssertKeyReachesHandlerAndIsStoredAsync(length);

    private async Task AssertKeyReachesHandlerAndIsStoredAsync(int length)
    {
        // Arrange - a fresh letters-only key, so no earlier case can replay a cached response
        var key = new string(Enumerable.Range(0, length).Select(_ => (char)('a' + Random.Shared.Next(26))).ToArray());
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Headers = { { "X-Idempotency-Key", key } },
            Content = JsonContent.Create(new CreateItemRequest { Name = "Key Length" })
        };

        // Act
        var response = await fixture.HttpClient!.SendAsync(httpRequest);

        // Assert - the handler ran, and exactly one row holds the full key
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        await using var dbContext = fixture.GetDbContext();
        var storedKeys = await dbContext.IdempotencyKeys
            .Where(k => k.IdempotentKey == key)
            .Select(k => k.IdempotentKey)
            .ToListAsync();
        storedKeys.ShouldBe([key]);
    }

    #endregion
}
