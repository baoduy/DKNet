// <copyright file="IdempotencyKeyCapTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using AspCore.Idempotency.ApiTests;
using AspCore.Idempotency.Tests.Fixtures;
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace AspCore.Idempotency.Tests.Filtering;

/// <summary>
///     DRK-2221 acceptance tests: idempotency keys are capped at 500 characters. The effective key limit is
///     the configured <c>MaxIdempotencyKeyLength</c> or 500, whichever is smaller. These cover the in-memory
///     store and the options rules; the SQL Server, PostgreSQL and Redis rows live in their store test projects.
/// </summary>
public sealed class IdempotencyKeyCapTests(ApiFixtureCachedResult fixture) : IClassFixture<ApiFixtureCachedResult>
{
    #region Methods

    [Theory]
    [Trait("Scenario", "A key within the effective limit is accepted")]
    [InlineData(null, 255)]
    [InlineData(500, 500)]
    [InlineData(1000, 300)]
    public async Task CreateOrder_KeyWithinEffectiveLimit_IsCreatedAndRetryGetsSameResponse(int? limit, int length)
    {
        // Arrange
        await using var app = AppWithKeyLimit(limit);
        using var client = app.CreateClient();
        var key = RandomKey(length);

        // Act
        var first = await CreateOrderAsync(client, key);
        var retry = await CreateOrderAsync(client, key);

        // Assert - the body carries a fresh Guid per handler run, so an identical body proves a replay
        var firstBody = await first.Content.ReadAsStringAsync();
        first.StatusCode.ShouldBe(HttpStatusCode.Created, firstBody);
        retry.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await retry.Content.ReadAsStringAsync()).ShouldBe(firstBody);
    }

    [Theory]
    [Trait("Scenario", "A key over the effective limit is refused")]
    [InlineData(null, 256,
        "The 'X-Idempotency-Key' header is invalid. Idempotency key must not exceed 255 characters.")]
    [InlineData(1000, 501,
        "The 'X-Idempotency-Key' header is invalid. Idempotency key must not exceed 500 characters.")]
    public async Task CreateOrder_KeyOverEffectiveLimit_Returns400NamingEffectiveLimitAndCreatesNoOrder(
        int? limit, int length, string expectedDetail)
    {
        // Arrange - a recording store registered last, so the filter resolves it instead of the in-memory one
        var store = new RecordingStore();
        await using var app = AppWithKeyLimit(limit, s => s.AddSingleton<IIdempotencyKeyStore>(store));
        using var client = app.CreateClient();
        var key = RandomKey(length);

        // Act
        var response = await CreateOrderAsync(client, key);

        // Assert - refused before any reservation, so the handler never ran and nothing was stored
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var detail = (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail!;
        detail.ShouldBe(expectedDetail);
        detail.ShouldNotContain(key);
        store.Calls.ShouldBe(0);
    }

    [Theory]
    [Trait("Scenario", "A key over the effective limit is refused")]
    [InlineData(null, 256, "Idempotency key must not exceed 255 characters.")]
    [InlineData(1000, 501, "Idempotency key must not exceed 500 characters.")]
    public void IsValid_KeyOverEffectiveLimit_SaysKeyMustNotExceedEffectiveLimit(
        int? limit, int length, string expectedMessage)
    {
        var options = new IdempotencyOptions();
        if (limit is not null) options.MaxIdempotencyKeyLength = limit.Value;

        var result = KeyInfo(length).IsValid(options);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(e => e.Message).ShouldBe([expectedMessage]);
    }

    [Fact]
    [Trait("Scenario", "The 500 cap holds in every store")]
    public async Task CreateOrder_InMemoryStoreWithLimit1000AndKeyOf501_Returns400BadRequest()
    {
        await using var app = AppWithKeyLimit(1000);
        using var client = app.CreateClient();

        var key = RandomKey(501);

        var response = await CreateOrderAsync(client, key);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var detail = (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail!;
        detail.ShouldBe("The 'X-Idempotency-Key' header is invalid. Idempotency key must not exceed 500 characters.");
        detail.ShouldNotContain(key);
    }

    // Input domain D3b: every IsValid failure appends its message to the 400 detail, never the submitted key.
    [Fact]
    public async Task CreateOrder_KeyWithInvalidFormat_Returns400NamingFormatRuleWithoutEchoingKey()
    {
        await using var app = AppWithKeyLimit(null);
        using var client = app.CreateClient();
        const string key = "order#42!";

        var response = await CreateOrderAsync(client, key);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var detail = (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail!;
        detail.ShouldBe(
            "The 'X-Idempotency-Key' header is invalid. " +
            "Idempotency key format is invalid. Allowed characters: alphanumeric, hyphens, underscores.");
        detail.ShouldNotContain(key);
    }

    [Fact]
    [Trait("Scenario", "An app that sets the limit above 500 still starts")]
    public async Task Start_WithLimit1000_StartsWithoutAnError()
    {
        await using var app = AppWithKeyLimit(1000);

        using var client = app.CreateClient();
        var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [Trait("Scenario", "A limit below 1 still stops the app at startup")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Start_WithLimitBelow1_StopsWithKeyLimitMustBeAtLeast1(int limit)
    {
        await using var app = AppWithKeyLimit(limit);

        var exception = Should.Throw<OptionsValidationException>(() => app.CreateClient());

        exception.Failures.ShouldBe(["MaxIdempotencyKeyLength must be at least 1."]);
    }

    // Input domain D1: a configured limit of 500 or less is the effective limit unchanged.
    [Theory]
    [InlineData(1, "Idempotency key must not exceed 1 characters.")]
    [InlineData(255, "Idempotency key must not exceed 255 characters.")]
    [InlineData(499, "Idempotency key must not exceed 499 characters.")]
    [InlineData(500, "Idempotency key must not exceed 500 characters.")]
    public void IsValid_ConfiguredLimitUpTo500_AcceptsKeyOfThatLengthAndRefusesOneLonger(
        int configured, string expectedMessage)
    {
        var options = new IdempotencyOptions { MaxIdempotencyKeyLength = configured };

        KeyInfo(configured).IsValid(options).IsFailed.ShouldBeFalse();
        KeyInfo(configured + 1).IsValid(options).Errors.Select(e => e.Message).ShouldBe([expectedMessage]);
    }

    // Input domain D2: a configured limit above 500 is capped at 500.
    [Theory]
    [InlineData(501)]
    [InlineData(1000)]
    [InlineData(int.MaxValue)]
    public void IsValid_ConfiguredLimitAbove500_AcceptsKeyOf500AndRefusesKeyOf501(int configured)
    {
        var options = new IdempotencyOptions { MaxIdempotencyKeyLength = configured };

        KeyInfo(500).IsValid(options).IsFailed.ShouldBeFalse();
        KeyInfo(501).IsValid(options).Errors.Select(e => e.Message)
            .ShouldBe(["Idempotency key must not exceed 500 characters."]);
    }

    /// <summary>
    ///     A host on the cached-result fixture with the key limit overridden; <c>null</c> keeps the default.
    ///     The override is per test, so the shared fixture's own configuration never changes.
    /// </summary>
    private WebApplicationFactory<ApiTests.Program> AppWithKeyLimit(
        int? limit, Action<IServiceCollection>? configure = null) =>
        fixture.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            if (limit is not null)
                s.PostConfigure<IdempotencyOptions>(o => o.MaxIdempotencyKeyLength = limit.Value);
            configure?.Invoke(s);
        }));

    private static Task<HttpResponseMessage> CreateOrderAsync(HttpClient client, string key) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Headers = { { "X-Idempotency-Key", key } },
            Content = JsonContent.Create(new CreateItemRequest { Name = "checkout-service order" })
        });

    private static IdempotentKeyInfo KeyInfo(int length) =>
        new() { IdempotentKey = new string('a', length), Endpoint = "/API/ORDERS", Method = "POST" };

    // Letters only, fresh per call, so no earlier case can replay a cached response.
    private static string RandomKey(int length) =>
        new(Enumerable.Range(0, length).Select(_ => (char)('a' + Random.Shared.Next(26))).ToArray());

    #endregion

    #region Nested Types

    private sealed class RecordingStore : IIdempotencyKeyStore
    {
        public int Calls { get; private set; }

        public ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo)
        {
            Calls++;
            return ValueTask.FromResult<(bool, CachedResponse?)>((false, null));
        }

        public ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse)
        {
            Calls++;
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
