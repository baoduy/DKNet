// <copyright file="IdempotencyIntegrationTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Net;
using System.Net.Http.Json;
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
    public async Task CreateItem_ConcurrentRequestsWithSameKey_OnlyOneProcessed()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Concurrent Item" };

        // Act - Send 5 concurrent requests with the same idempotency key
        var tasks = Enumerable.Range(0, 5).Select(_ =>
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/items")
            {
                Headers = { { "Idempotency-Key", idempotencyKey } },
                Content = JsonContent.Create(request)
            };
            return fixture.HttpClient!.SendAsync(httpRequest);
        }).ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert - All should return 201
        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Created);

        var items = await Task.WhenAll(
            responses.Select(async r => await r.Content.ReadFromJsonAsync<CreateItemResponse>()));

        // All items should have the same ID (from first processed request)
        var firstItemId = items.First()!.Id;
        items.ShouldAllBe(i => i!.Id == firstItemId);

        // Verify the key exists in database (at least one entry for this key)
        await using var dbContext = fixture.GetDbContext();
        var keyExists = await dbContext.IdempotencyKeys
            .AnyAsync(k => k.Key == idempotencyKey.ToUpperInvariant());
        keyExists.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateItem_VerifyDatabaseSchema_HasCorrectIndexes()
    {
        // Arrange & Act
        await using var dbContext = fixture.GetDbContext();

        // Query the database schema for indexes
        var sql = @"
            SELECT i.name 
            FROM sys.indexes i
            INNER JOIN sys.objects o ON i.object_id = o.object_id
            WHERE o.name = 'IdempotencyKeys'";

        var indexes = await dbContext.Database.SqlQueryRaw<string>(sql).ToListAsync();

        // Assert - Verify required indexes exist
        indexes.ShouldContain("UX_IdempotencyKey_Composite");
        indexes.ShouldContain("IX_IdempotencyKeys_ExpiresAt");
        indexes.ShouldContain("IX_IdempotencyKeys_Route_CreatedAt");
    }

    [Fact]
    public async Task CreateItem_VerifyKeySanitization_RemovesInvalidCharacters()
    {
        // Arrange
        var dirtyKey = "test-key-123!@#$%^&*()";
        var request = new CreateItemRequest { Name = "Sanitization Test" };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Headers = { { "Idempotency-Key", dirtyKey } },
            Content = JsonContent.Create(request)
        };

        // Act
        var response = await fixture.HttpClient!.SendAsync(httpRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Verify sanitized key in database (only alphanumeric and hyphens)
        await using var dbContext = fixture.GetDbContext();
        var storedKey = await dbContext.IdempotencyKeys
            .FirstOrDefaultAsync(k => k.Key.StartsWith("TESTKEY123"));
        storedKey.ShouldNotBeNull();
        storedKey.Key.ShouldMatch(@"^[A-Z0-9\-]+$"); // Sanitized and uppercased
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
            Headers = { { "Idempotency-Key", idempotencyKey1 } },
            Content = JsonContent.Create(request)
        };
        var response1 = await fixture.HttpClient!.SendAsync(httpRequest1);
        var item1 = await response1.Content.ReadFromJsonAsync<CreateItemResponse>();

        // Second request with different key
        var httpRequest2 = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Headers = { { "Idempotency-Key", idempotencyKey2 } },
            Content = JsonContent.Create(request)
        };
        var response2 = await fixture.HttpClient!.SendAsync(httpRequest2);
        var item2 = await response2.Content.ReadFromJsonAsync<CreateItemResponse>();

        // Assert
        response1.StatusCode.ShouldBe(HttpStatusCode.Created);
        response2.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Should create two different items
        item1!.Id.ShouldNotBe(item2!.Id);

        // Verify two entries in database
        await using var dbContext = fixture.GetDbContext();
        var count = await dbContext.IdempotencyKeys.CountAsync();
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
            Headers = { { "Idempotency-Key", idempotencyKey } },
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
        var dbContext = fixture.GetDbContext();
        var storedKey = await dbContext.IdempotencyKeys
            .FirstOrDefaultAsync(k => k.Key == idempotencyKey.ToUpperInvariant());

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
            Headers = { { "Idempotency-Key", idempotencyKey } },
            Content = JsonContent.Create(request)
        };

        // Act
        await fixture.HttpClient!.SendAsync(httpRequest);

        // Assert
        await using var dbContext = fixture.GetDbContext();
        var storedKey = await dbContext.IdempotencyKeys
            .FirstAsync(k => k.Key == idempotencyKey.ToUpperInvariant());

        storedKey.StatusCode.ShouldBe(201);
        storedKey.ContentType!.ShouldContain("application/json");
        storedKey.Body.ShouldNotBeNullOrWhiteSpace();
        storedKey.CreatedAt.ShouldBeInRange(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow);
        //storedKey.ExpiresAt.ShouldBeGreaterThan(storedKey.CreatedAt);
    }

    [Fact]
    public async Task CreateItem_WithoutIdempotencyKey_ProcessesNormally()
    {
        // Arrange
        var request = new CreateItemRequest { Name = "No Key Item" };

        // Act - Request WITHOUT idempotency key
        var response = await fixture.HttpClient!.PostAsJsonAsync("/api/items", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var item = await response.Content.ReadFromJsonAsync<CreateItemResponse>();
        item.ShouldNotBeNull();
        item.Name.ShouldBe("No Key Item");
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
            Headers = { { "Idempotency-Key", idempotencyKey } },
            Content = JsonContent.Create(request)
        };
        var response1 = await fixture.HttpClient!.SendAsync(httpRequest1);
        var item1 = await response1.Content.ReadFromJsonAsync<CreateItemResponse>();

        // Act - Second request with same idempotency key
        var httpRequest2 = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Headers = { { "Idempotency-Key", idempotencyKey } },
            Content = JsonContent.Create(request)
        };
        var response2 = await fixture.HttpClient!.SendAsync(httpRequest2);

        // Assert
        response2.StatusCode.ShouldBe(HttpStatusCode.Created);

        var item2 = await response2.Content.ReadFromJsonAsync<CreateItemResponse>();
        item2.ShouldNotBeNull();

        // Both responses should be identical (cached)
        item2.Id.ShouldBe(item1!.Id);
        item2.Name.ShouldBe(item1.Name);

        // Verify only ONE entry in database
        await using var dbContext = fixture.GetDbContext();
        var count = await dbContext.IdempotencyKeys
            .CountAsync(k => k.Key == idempotencyKey.ToUpperInvariant());
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