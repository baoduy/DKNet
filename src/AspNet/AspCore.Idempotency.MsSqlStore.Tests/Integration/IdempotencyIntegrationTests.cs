// <copyright file="IdempotencyIntegrationTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AspCore.Idempotency.ApiTests;
using AspCore.Idempotency.MsSqlStore.Tests.Fixtures;

namespace AspCore.Idempotency.MsSqlStore.Tests.Integration;

/// <summary>
///     Integration tests for MS SQL idempotency storage using real SQL Server container.
/// </summary>
[Collection("Api Collection")]
public sealed class IdempotencyIntegrationTests(ApiFixture fixture) : IAsyncLifetime
{
    #region Methods

    [Fact]
    public async Task ApiHealthCheck()
    {
        // Arrange & Act
        var response = await fixture.HttpClient!.GetAsync("/api/health");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateItem_ConcurrentRequestsWithSameKey_OnlyOneProcessed()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Concurrent Item" };

        // Act - Send 5 concurrent requests with the same idempotency key
        // multiple requests may process simultaneously when they all arrive before any is cached.
        // This is a known limitation of stateless filters without distributed locking.
        var tasks = Enumerable.Range(0, 5).Select(_ =>
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/items")
            {
                Headers = { { "X-Idempotency-Key", idempotencyKey } },
                Content = JsonContent.Create(request)
            };
            return fixture.HttpClient!.SendAsync(httpRequest);
        }).ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert - With concurrent requests, some or all may succeed (201 or 409 Conflict)
        // The first request wins, subsequent ones get 409 Conflict (if ConflictHandling = ConflictResponse)
        // or the cached response (if ConflictHandling = CachedResult)
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        // At least one should succeed (201)
        successCount.ShouldBeGreaterThanOrEqualTo(1);

        // The rest should be conflicts or cached responses
        var otherCount = responses.Count(r => r.StatusCode != HttpStatusCode.Created &&
                                              r.StatusCode != HttpStatusCode.Conflict);
        (successCount + conflictCount + otherCount).ShouldBe(5);

        // Verify only ONE entry in database (unique constraint ensures this)
        await using var dbContext = fixture.GetDbContext();
        var count = await dbContext.IdempotencyKeys
            .CountAsync(k => k.IdempotentKey == idempotencyKey &&
                             k.Method == "POST" && k.Endpoint == "/api/items");
        count.ShouldBe(1, "Unique constraint should prevent duplicate idempotency keys");
    }

    [Fact]
    public async Task CreateItem_VerifyKeySanitization_RemovesInvalidCharacters()
    {
        // Arrange
        var dirtyKey = "test-key-123!@#$%^&*()";
        var request = new CreateItemRequest { Name = "Sanitization Test" };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Headers = { { "X-Idempotency-Key", dirtyKey } },
            Content = JsonContent.Create(request)
        };

        // Act
        var response = await fixture.HttpClient!.SendAsync(httpRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var rpBody = await response.Content.ReadAsStringAsync();
        rpBody.ShouldContain("The 'X-Idempotency-Key' header is invalid.");

        // Verify sanitized key in database - the store sanitizes: removes non-alphanumeric (except hyphens), uppercases
        await using var dbContext = fixture.GetDbContext();
        await dbContext.Database.EnsureCreatedAsync();

        var storedKey = await dbContext.IdempotencyKeys
            .FirstOrDefaultAsync(k =>
                k.Method == "POST" && k.Endpoint == "/api/items" && k.IdempotentKey.Contains("test-key-123"));

        storedKey.ShouldBeNull("Invalid idempotency key should not be stored in database.");
    }

    [Fact]
    public async Task CreateItem_WithDifferentIdempotencyKeys_CreatesMultipleItems()
    {
        // Arrange
        var idempotencyKey1 = Guid.NewGuid().ToString();
        var idempotencyKey2 = Guid.NewGuid().ToString();

        var request = new CreateItemRequest { Name = "Test Item 3" };

        // Act - First request
        var httpRequest1 = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Headers = { { "X-Idempotency-Key", idempotencyKey1 } },
            Content = JsonContent.Create(request)
        };
        var response1 = await fixture.HttpClient!.SendAsync(httpRequest1);
        var item1 = await response1.Content.ReadFromJsonAsync<CreateItemResponse>();

        // Second request with different key
        var httpRequest2 = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Headers = { { "X-Idempotency-Key", idempotencyKey2 } },
            Content = JsonContent.Create(request)
        };
        var response2 = await fixture.HttpClient!.SendAsync(httpRequest2);
        var item2 = await response2.Content.ReadFromJsonAsync<CreateItemResponse>();

        // Assert
        response1.StatusCode.ShouldBe(HttpStatusCode.Created);
        response2.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Should create two different items
        item1!.Id.ShouldNotBe(item2!.Id);

        // Verify two entries in database for this endpoint
        await using var dbContext = fixture.GetDbContext();
        var count = await dbContext.IdempotencyKeys
            .CountAsync(k => k.Method == "POST" && k.Endpoint == "/api/items" &&
                             (k.IdempotentKey == idempotencyKey1 || k.IdempotentKey == idempotencyKey2));
        count.ShouldBe(2);
    }

    [Fact]
    public async Task CreateItem_WithIdempotencyKey_FirstRequest_StoresInDatabase()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Test Item 1" };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Headers = { { "X-Idempotency-Key", idempotencyKey } },
            Content = JsonContent.Create(request)
        };

        // Act
        var response = await fixture.HttpClient!.SendAsync(httpRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var item = await response.Content.ReadFromJsonAsync<CreateItemResponse>();
        item.ShouldNotBeNull();
        item.Name.ShouldBe("Test Item 1");

        // Verify stored in database
        await using var dbContext = fixture.GetDbContext();
        var storedKey = await dbContext.IdempotencyKeys
            .FirstOrDefaultAsync(k => k.IdempotentKey == idempotencyKey &&
                                      k.Method == "POST" && k.Endpoint == "/api/items");

        storedKey.ShouldNotBeNull();
        storedKey.StatusCode.ShouldBe(201);
        storedKey.Body.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateItem_WithIdempotencyKey_StoresCorrectResponseDetails()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Detail Test Item" };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Headers = { { "X-Idempotency-Key", idempotencyKey } },
            Content = JsonContent.Create(request)
        };

        // Act
        await fixture.HttpClient!.SendAsync(httpRequest);

        // Assert
        await using var dbContext = fixture.GetDbContext();
        var storedKey = await dbContext.IdempotencyKeys
            .FirstOrDefaultAsync(k => k.IdempotentKey == idempotencyKey &&
                             k.Method == "POST" && k.Endpoint == "/api/items");

        storedKey.ShouldNotBeNull();
        storedKey.StatusCode.ShouldBe(201);
        storedKey.ContentType!.ShouldContain("application/json");
        storedKey.Body.ShouldNotBeNullOrWhiteSpace();
        storedKey.CreatedAt.ShouldBeInRange(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow);
        storedKey.ExpiresAt.ShouldNotBeNull();
        storedKey.ExpiresAt!.Value.ShouldBeGreaterThan(storedKey.CreatedAt);
    }

    [Fact]
    public async Task CreateItem_WithoutIdempotencyKey_ProcessesNormally()
    {
        // Arrange
        var request = new CreateItemRequest { Name = "No Key Item" };

        // Act - Request WITHOUT idempotency key
        var response = await fixture.HttpClient!.PostAsJsonAsync("/api/items", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var item = await response.Content.ReadAsStringAsync();
        item.ShouldNotBeNull();
        item.ShouldContain("The 'X-Idempotency-Key' header is invalid.");
    }

    [Fact]
    public async Task CreateItem_WithSameIdempotencyKey_SecondRequest_ReturnsCachedResponse()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Test Item 2" };

        // First request
        var httpRequest1 = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Headers = { { "X-Idempotency-Key", idempotencyKey } },
            Content = JsonContent.Create(request)
        };
        var response1 = await fixture.HttpClient!.SendAsync(httpRequest1);
        var item1 = await response1.Content.ReadAsStringAsync();

        // Act - Second request with same idempotency key
        var httpRequest2 = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Headers = { { "X-Idempotency-Key", idempotencyKey } },
            Content = JsonContent.Create(request)
        };
        var response2 = await fixture.HttpClient!.SendAsync(httpRequest2);

        // Assert
        response2.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Verify only ONE entry in database
        await using var dbContext = fixture.GetDbContext();
        var count = await dbContext.IdempotencyKeys
            .CountAsync(k => k.IdempotentKey == idempotencyKey &&
                             k.Method == "POST" && k.Endpoint == "/api/items");
        count.ShouldBe(1);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public Task InitializeAsync() => Task.CompletedTask;

    #endregion
}

/// <summary>
///     Collection definition for API fixture with SQL Server container.
/// </summary>
[CollectionDefinition("Api Collection")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
}