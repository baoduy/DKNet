// <copyright file="IdempotencyRedisKeyCapTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using AspCore.Idempotency.ApiTests;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;

namespace AspCore.Idempotency.RedisStore.Tests.Unit;

/// <summary>
///     DRK-2221 acceptance test, Redis row of "The 500 cap holds in every store": an app on the Redis store
///     with a key limit of 1000 refuses a 501-character key. The filter rejects the key before any store call,
///     so the Redis database is a Moq fake that must see no command at all.
/// </summary>
public sealed class IdempotencyRedisKeyCapTests
{
    #region Methods

    [Fact]
    [Trait("Scenario", "The 500 cap holds in every store")]
    public async Task CreateOrder_RedisStoreWithLimit1000AndKeyOf501_Returns400BadRequest()
    {
        // Arrange - the fake multiplexer is registered first, so AddIdempotencyWithRedisStore never connects
        var database = new Mock<IDatabase>();
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(database.Object);

        await using var app = new WebApplicationFactory<ApiTests.Program>().WithWebHostBuilder(b =>
            b.ConfigureServices(s => s
                .AddIdempotencyRedisStore(multiplexer.Object)
                .AddIdempotencyWithRedisStore("localhost:6379", o => o.MaxIdempotencyKeyLength = 1000)));
        using var client = app.CreateClient();
        var key = new string(Enumerable.Range(0, 501).Select(_ => (char)('a' + Random.Shared.Next(26))).ToArray());

        // Act
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Headers = { { "X-Idempotency-Key", key } },
            Content = JsonContent.Create(new CreateItemRequest { Name = "checkout-service order" })
        });

        // Assert - the Redis store is the one wired, and it never received a command
        app.Services.GetRequiredService<IIdempotencyKeyStore>().ShouldBeOfType<IdempotencyRedisStore>();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        database.Invocations.ShouldBeEmpty();
    }

    #endregion
}
