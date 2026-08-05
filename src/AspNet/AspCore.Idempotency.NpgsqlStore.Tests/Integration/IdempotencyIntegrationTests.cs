// <copyright file="IdempotencyIntegrationTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using AspCore.Idempotency.ApiTests;
using AspCore.Idempotency.NpgsqlStore.Tests.Fixtures;
using DKNet.AspCore.Idempotency;

namespace AspCore.Idempotency.NpgsqlStore.Tests.Integration;

/// <summary>
///     Integration tests for PostgreSQL idempotency storage using a real PostgreSQL container.
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
    public async Task ApiFixture_UsesIsolatedDatabaseConnectionString()
    {
        // Arrange
        await using var dbContext = fixture.GetDbContext();

        // Act
        var connectionString = dbContext.Database.GetConnectionString();

        // Assert
        connectionString.ShouldNotBeNullOrWhiteSpace();
        connectionString.ShouldContain(fixture.DatabaseName);
        connectionString.ShouldNotContain("Database=postgres");
    }

    [Fact]
    public async Task CreateItem_ConcurrentRequestsWithSameKey_OnlyOneProcessed()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Concurrent Item" };

        // Act - Send 5 concurrent requests with the same idempotency key.
        // The store's atomic reservation ensures at most one of them ever reaches the handler.
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

        // Assert - the fixture uses ConflictHandling = CachedResult, so a duplicate arriving after the
        // winner completes legitimately gets 201 with the replayed body, not 409; we don't assert an
        // exact 201/409 split. Instead: the handler generates a fresh Guid per call, so every 201
        // response carrying the SAME Id proves the handler ran exactly once.
        var createdIds = new List<Guid>();
        foreach (var response in responses)
        {
            if (response.StatusCode != HttpStatusCode.Created) continue;
            var item = await response.Content.ReadFromJsonAsync<CreateItemResponse>();
            createdIds.Add(item!.Id);
        }

        createdIds.ShouldNotBeEmpty("At least one request should have succeeded");
        createdIds.Distinct().Count().ShouldBe(1, "The handler must have executed exactly once");

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
        await fixture.HttpClient!.SendAsync(httpRequest1);

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

    [Fact]
    public async Task CreateItem_WithExpiredIdempotencyKey_ProcessesAsNewRequest()
    {
        // Arrange - insert an already-expired entry directly, bypassing the HTTP pipeline
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Expired Key Item" };

        await using (var seedDbContext = fixture.GetDbContext())
        {
            var expiredEntity = new IdempotencyKeyEntity(
                new IdempotentKeyInfo
                {
                    IdempotentKey = idempotencyKey,
                    Endpoint = "/api/items",
                    Method = "POST"
                },
                new CachedResponse
                {
                    StatusCode = 201,
                    Body = "{}",
                    ContentType = "application/json",
                    CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
                });

            seedDbContext.IdempotencyKeys.Add(expiredEntity);
            await seedDbContext.SaveChangesAsync();
        }

        // Act - a request with the same key should be processed as new, not served the expired cache entry
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Headers = { { "X-Idempotency-Key", idempotencyKey } },
            Content = JsonContent.Create(request)
        };
        var response = await fixture.HttpClient!.SendAsync(httpRequest);

        // Assert - a served-from-cache response would carry the seeded entry's empty "{}" body and no name;
        // getting the real handler's output back proves the expired key was treated as unprocessed and replayed.
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var item = await response.Content.ReadFromJsonAsync<CreateItemResponse>();
        item!.Name.ShouldBe("Expired Key Item");
        item.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateItem_WithExpiredInFlightReservation_ProcessesAsNewRequest()
    {
        // Arrange - seed an already-expired StatusCode=102 reservation placeholder directly, simulating
        // a prior request whose handler never completed (crashed, timed out) within the reservation window.
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Expired Reservation Item" };

        await using (var seedDbContext = fixture.GetDbContext())
        {
            var expiredReservation = new IdempotencyKeyEntity(
                new IdempotentKeyInfo
                {
                    IdempotentKey = idempotencyKey,
                    Endpoint = "/api/items",
                    Method = "POST"
                },
                new CachedResponse
                {
                    StatusCode = 102,
                    Body = null,
                    ContentType = "application/json",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
                });

            seedDbContext.IdempotencyKeys.Add(expiredReservation);
            await seedDbContext.SaveChangesAsync();
        }

        // Act - a request with the same key must not be permanently blocked by the stale reservation
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Headers = { { "X-Idempotency-Key", idempotencyKey } },
            Content = JsonContent.Create(request)
        };
        var response = await fixture.HttpClient!.SendAsync(httpRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var item = await response.Content.ReadFromJsonAsync<CreateItemResponse>();
        item!.Name.ShouldBe("Expired Reservation Item");
        item.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateItem_ConcurrentRequestsAgainstExpiredReservation_OnlyOneProcessed()
    {
        // Arrange - seed an already-expired StatusCode=102 reservation so every concurrent request's
        // initial unexpired-row query misses it, and every request's own reservation INSERT collides
        // with this same stale row (unique index doesn't care that it's expired).
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Expired Reservation Race Item" };

        await using (var seedDbContext = fixture.GetDbContext())
        {
            var expiredReservation = new IdempotencyKeyEntity(
                new IdempotentKeyInfo
                {
                    IdempotentKey = idempotencyKey,
                    Endpoint = "/api/items",
                    Method = "POST"
                },
                new CachedResponse
                {
                    StatusCode = 102,
                    Body = null,
                    ContentType = "application/json",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
                });

            seedDbContext.IdempotencyKeys.Add(expiredReservation);
            await seedDbContext.SaveChangesAsync();
        }

        // Act - fire 5 concurrent requests against the same key, all racing the stale expired row
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

        // Assert - only one distinct handler execution must be observable, same as the fresh-key case
        var createdIds = new List<Guid>();
        foreach (var response in responses)
        {
            if (response.StatusCode != HttpStatusCode.Created) continue;
            var item = await response.Content.ReadFromJsonAsync<CreateItemResponse>();
            createdIds.Add(item!.Id);
        }

        createdIds.ShouldNotBeEmpty("At least one request should have succeeded");
        createdIds.Distinct().Count().ShouldBe(1,
            "The handler must have executed exactly once, even when racing an already-expired reservation row");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public Task InitializeAsync() => Task.CompletedTask;

    #endregion
}

/// <summary>
///     Collection definition for API fixture with PostgreSQL container.
/// </summary>
[CollectionDefinition("Api Collection")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
}
