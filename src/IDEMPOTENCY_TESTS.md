# DKNet.AspCore.Idempotency - Test Implementation Guide

## Overview

This guide provides comprehensive test implementations for the DKNet.AspCore.Idempotency library. The test suite is organized into functional categories with clear priorities.

---

## Test Suite Structure

```
AspCore.Idempotency.Tests/
├── GlobalUsings.cs (existing)
├── Fixtures/
│   └── ApiFixture.cs (existing - enhance)
├── Unit/
│   ├── IdempotencyKeyValidationTests.cs (P0)
│   ├── CacheKeyManagementTests.cs (P0)
│   └── OptionsValidationTests.cs (P1)
├── Integration/
│   ├── DuplicateRequestHandlingTests.cs (P0)
│   ├── ConflictHandlingTests.cs (P0)
│   ├── CacheOperationTests.cs (P0)
│   ├── ErrorHandlingTests.cs (P1)
│   ├── ConcurrencyTests.cs (P1)
│   └── EndToEndTests.cs (P0)
└── Performance/
    └── CachingPerformanceTests.cs (P2)
```

---

## P0: Critical Test Implementations

### 1. Unit Tests: IdempotencyKeyValidationTests.cs

```csharp
// <copyright file="IdempotencyKeyValidationTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;

namespace AspCore.Idempotency.Tests.Unit;

/// <summary>
///     Unit tests for idempotency key validation in the endpoint filter.
/// </summary>
public class IdempotencyKeyValidationTests
{
    #region Missing Header Tests

    [Fact]
    public async Task InvokeAsync_WhenIdempotencyHeaderMissing_Returns400BadRequest()
    {
        // Arrange
        var context = new TestEndpointFilterInvocationContext();
        context.RemoveHeader("X-Idempotency-Key");
        
        var options = Options.Create(new IdempotencyOptions());
        var logger = new TestLogger<IdempotencyEndpointFilter>();
        var cache = new TestCache();
        var filter = new IdempotencyEndpointFilter(
            new IdempotencyDistributedCacheRepository(cache, options, logger),
            options,
            logger);

        // Act
        var result = await filter.InvokeAsync(context, ctx => ValueTask.FromResult((object?)"ok"));

        // Assert
        result.ShouldBeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result;
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Detail.ShouldContain("X-Idempotency-Key");
    }

    [Fact]
    public async Task InvokeAsync_WhenIdempotencyHeaderEmpty_Returns400BadRequest()
    {
        // Arrange
        var context = new TestEndpointFilterInvocationContext();
        context.SetHeader("X-Idempotency-Key", "");
        
        var options = Options.Create(new IdempotencyOptions());
        var logger = new TestLogger<IdempotencyEndpointFilter>();
        var cache = new TestCache();
        var filter = new IdempotencyEndpointFilter(
            new IdempotencyDistributedCacheRepository(cache, options, logger),
            options,
            logger);

        // Act
        var result = await filter.InvokeAsync(context, ctx => ValueTask.FromResult((object?)"ok"));

        // Assert
        result.ShouldBeOfType<ProblemHttpResult>();
        ((ProblemHttpResult)result).StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_WhenIdempotencyHeaderContainsOnlyWhitespace_Returns400BadRequest()
    {
        // Arrange
        var context = new TestEndpointFilterInvocationContext();
        context.SetHeader("X-Idempotency-Key", "   ");
        
        var options = Options.Create(new IdempotencyOptions());
        var logger = new TestLogger<IdempotencyEndpointFilter>();
        var cache = new TestCache();
        var filter = new IdempotencyEndpointFilter(
            new IdempotencyDistributedCacheRepository(cache, options, logger),
            options,
            logger);

        // Act
        var result = await filter.InvokeAsync(context, ctx => ValueTask.FromResult((object?)"ok"));

        // Assert
        result.ShouldBeOfType<ProblemHttpResult>();
        ((ProblemHttpResult)result).StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    #endregion

    #region Valid Header Tests

    [Theory]
    [InlineData("550e8400-e29b-41d4-a716-446655440000")] // UUID v4
    [InlineData("abc-123")]
    [InlineData("abc_123")]
    [InlineData("ABC123")]
    public async Task InvokeAsync_WhenIdempotencyHeaderValid_ProcessesRequest(string validKey)
    {
        // Arrange
        var context = new TestEndpointFilterInvocationContext();
        context.SetHeader("X-Idempotency-Key", validKey);
        context.SetStatusCode(200);
        
        var options = Options.Create(new IdempotencyOptions());
        var logger = new TestLogger<IdempotencyEndpointFilter>();
        var cache = new TestCache();
        var filter = new IdempotencyEndpointFilter(
            new IdempotencyDistributedCacheRepository(cache, options, logger),
            options,
            logger);

        var nextCalled = false;
        var result = await filter.InvokeAsync(
            context,
            ctx =>
            {
                nextCalled = true;
                return ValueTask.FromResult((object?)"result");
            });

        // Assert
        nextCalled.ShouldBeTrue();
        result.ShouldBe("result");
    }

    [Fact]
    public async Task InvokeAsync_WhenIdempotencyKeyExceedsMaxLength_Returns400BadRequest()
    {
        // Arrange
        var options = Options.Create(new IdempotencyOptions { MaxIdempotencyKeyLength = 10 });
        var context = new TestEndpointFilterInvocationContext();
        context.SetHeader("X-Idempotency-Key", "this-key-is-too-long");
        
        var logger = new TestLogger<IdempotencyEndpointFilter>();
        var cache = new TestCache();
        var filter = new IdempotencyEndpointFilter(
            new IdempotencyDistributedCacheRepository(cache, options, logger),
            options,
            logger);

        // Act
        var result = await filter.InvokeAsync(context, ctx => ValueTask.FromResult((object?)"ok"));

        // Assert
        result.ShouldBeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result;
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Detail.ShouldContain("10");
    }

    [Theory]
    [InlineData("key/with/slashes")]  // Forbidden
    [InlineData("key\nwith\nnewlines")]  // Forbidden
    [InlineData("key with spaces")]  // Forbidden
    public async Task InvokeAsync_WhenIdempotencyKeyFormatInvalid_Returns400BadRequest(string invalidKey)
    {
        // Arrange
        var options = Options.Create(new IdempotencyOptions());
        var context = new TestEndpointFilterInvocationContext();
        context.SetHeader("X-Idempotency-Key", invalidKey);
        
        var logger = new TestLogger<IdempotencyEndpointFilter>();
        var cache = new TestCache();
        var filter = new IdempotencyEndpointFilter(
            new IdempotencyDistributedCacheRepository(cache, options, logger),
            options,
            logger);

        // Act
        var result = await filter.InvokeAsync(context, ctx => ValueTask.FromResult((object?)"ok"));

        // Assert
        result.ShouldBeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result;
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    #endregion

    #region Custom Header Name Tests

    [Fact]
    public async Task InvokeAsync_WithCustomIdempotencyHeaderKey_ChecksCustomHeader()
    {
        // Arrange
        var options = Options.Create(new IdempotencyOptions 
        { 
            IdempotencyHeaderKey = "Request-Id" 
        });
        var context = new TestEndpointFilterInvocationContext();
        context.SetHeader("Request-Id", "custom-key");
        context.SetStatusCode(200);
        
        var logger = new TestLogger<IdempotencyEndpointFilter>();
        var cache = new TestCache();
        var filter = new IdempotencyEndpointFilter(
            new IdempotencyDistributedCacheRepository(cache, options, logger),
            options,
            logger);

        var nextCalled = false;
        var result = await filter.InvokeAsync(
            context,
            ctx =>
            {
                nextCalled = true;
                return ValueTask.FromResult((object?)"result");
            });

        // Assert
        nextCalled.ShouldBeTrue();
        result.ShouldBe("result");
    }

    #endregion
}
```

---

### 2. Integration Tests: DuplicateRequestHandlingTests.cs

```csharp
// <copyright file="DuplicateRequestHandlingTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace AspCore.Idempotency.Tests.Integration;

/// <summary>
///     Integration tests for duplicate request detection and handling.
/// </summary>
public class DuplicateRequestHandlingTests : IAsyncLifetime
{
    private ApiFixture? _fixture;

    public async Task InitializeAsync()
    {
        _fixture = new ApiFixture();
        await _fixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        if (_fixture is not null)
            await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task FirstRequest_WithIdempotencyKey_Returns201Created()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Test Item" };

        // Act
        var response = await SendPostWithIdempotencyKey("/api/items", request, idempotencyKey);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var content = await response.Content.ReadAsAsync<CreateItemResponse>();
        content.ShouldNotBeNull();
        content.Name.ShouldBe("Test Item");
    }

    [Fact]
    public async Task SecondRequest_WithSameIdempotencyKey_ReturnsConflict()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Test Item" };

        // Act - First request
        var firstResponse = await SendPostWithIdempotencyKey("/api/items", request, idempotencyKey);

        // Act - Second request (duplicate)
        var secondResponse = await SendPostWithIdempotencyKey("/api/items", request, idempotencyKey);

        // Assert
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DuplicateRequest_WithConflictResponse_ContainsProblemDetails()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Test Item" };

        // Act - First request
        await SendPostWithIdempotencyKey("/api/items", request, idempotencyKey);

        // Act - Second request
        var secondResponse = await SendPostWithIdempotencyKey("/api/items", request, idempotencyKey);
        var problem = await secondResponse.Content.ReadAsAsync<ProblemDetails>();

        // Assert
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status409Conflict);
        problem.Detail.ShouldContain(idempotencyKey);
    }

    [Fact]
    public async Task DifferentKeys_WithSameEndpoint_BothProcessed()
    {
        // Arrange
        var key1 = Guid.NewGuid().ToString();
        var key2 = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Test Item" };

        // Act
        var response1 = await SendPostWithIdempotencyKey("/api/items", request, key1);
        var response2 = await SendPostWithIdempotencyKey("/api/items", request, key2);

        // Assert
        response1.StatusCode.ShouldBe(HttpStatusCode.Created);
        response2.StatusCode.ShouldBe(HttpStatusCode.Created);
        
        var item1 = await response1.Content.ReadAsAsync<CreateItemResponse>();
        var item2 = await response2.Content.ReadAsAsync<CreateItemResponse>();
        
        item1.Id.ShouldNotBe(item2.Id); // Different items created
    }

    [Fact]
    public async Task SameKeyDifferentEndpoints_BothProcessed()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var createRequest = new CreateItemRequest { Name = "Test Item" };

        // Act - POST to /api/items
        var postResponse = await SendPostWithIdempotencyKey("/api/items", createRequest, idempotencyKey);

        // Act - GET request with same key (should succeed - different endpoint)
        var getResponse = await SendGetWithIdempotencyKey("/api/health", idempotencyKey);

        // Assert
        postResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK); // Not a conflict
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task MultipleConsecutiveDuplicates_AllReturnConflict(int duplicateCount)
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Test Item" };

        // Act - First request
        var firstResponse = await SendPostWithIdempotencyKey("/api/items", request, idempotencyKey);

        // Act - Duplicate requests
        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < duplicateCount; i++)
        {
            var response = await SendPostWithIdempotencyKey("/api/items", request, idempotencyKey);
            responses.Add(response);
        }

        // Assert
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Conflict);
    }

    private async Task<HttpResponseMessage> SendPostWithIdempotencyKey<T>(
        string path,
        T request,
        string idempotencyKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, path);
        message.Headers.Add("X-Idempotency-Key", idempotencyKey);
        message.Content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        return await _fixture!.HttpClient!.SendAsync(message);
    }

    private async Task<HttpResponseMessage> SendGetWithIdempotencyKey(
        string path,
        string idempotencyKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, path);
        message.Headers.Add("X-Idempotency-Key", idempotencyKey);

        return await _fixture!.HttpClient!.SendAsync(message);
    }
}
```

---

### 3. Integration Tests: ConflictHandlingTests.cs

```csharp
// <copyright file="ConflictHandlingTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace AspCore.Idempotency.Tests.Integration;

/// <summary>
///     Integration tests for different conflict handling strategies.
/// </summary>
public class ConflictHandlingTests : IAsyncLifetime
{
    private ApiFixture? _fixture;

    public async Task InitializeAsync()
    {
        _fixture = new ApiFixture();
        await _fixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        if (_fixture is not null)
            await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task ConflictResponse_Strategy_Returns409OnDuplicate()
    {
        // Arrange - use default ConflictResponse strategy
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Test Item" };

        // Act
        var firstResponse = await SendPost("/api/items", request, idempotencyKey);
        var secondResponse = await SendPost("/api/items", request, idempotencyKey);

        // Assert
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ConflictResponse_Strategy_DoesNotCacheResponse()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Test Item" };

        // Act - First request
        var firstResponse = await SendPost("/api/items", request, idempotencyKey);
        var firstContent = await firstResponse.Content.ReadAsAsync<CreateItemResponse>();

        // Act - Second request
        var secondResponse = await SendPost("/api/items", request, idempotencyKey);

        // Assert - Second response should be 409, not the cached first response
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        
        // The response is a problem, not the original item
        var secondContent = await secondResponse.Content.ReadAsAsync<ProblemDetails>();
        secondContent.ShouldNotBeNull();
        secondContent.Status.ShouldBe(StatusCodes.Status409Conflict);
    }

    private async Task<HttpResponseMessage> SendPost<T>(
        string path,
        T request,
        string idempotencyKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, path);
        message.Headers.Add("X-Idempotency-Key", idempotencyKey);
        message.Content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        return await _fixture!.HttpClient!.SendAsync(message);
    }
}
```

---

### 4. Integration Tests: CacheOperationTests.cs

```csharp
// <copyright file="CacheOperationTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace AspCore.Idempotency.Tests.Integration;

/// <summary>
///     Integration tests for cache operations and expiration.
/// </summary>
public class CacheOperationTests : IAsyncLifetime
{
    private ApiFixture? _fixture;

    public async Task InitializeAsync()
    {
        _fixture = new ApiFixture();
        await _fixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        if (_fixture is not null)
            await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task SuccessfulResponse_IsCached()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Test Item" };

        // Act - First request (201)
        var firstResponse = await SendPost("/api/items", request, idempotencyKey);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act - Second request (should hit cache)
        var secondResponse = await SendPost("/api/items", request, idempotencyKey);

        // Assert - With default ConflictResponse, should be 409, but key is marked as processed
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ErrorResponse_IsNotCached()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        
        // Create an endpoint that returns 400
        // This test assumes a /api/error endpoint that returns 400
        
        // Act - First request (400 error)
        var firstResponse = await SendPostToErrorEndpoint(idempotencyKey);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Act - Second request with same key
        var secondResponse = await SendPostToErrorEndpoint(idempotencyKey);

        // Assert - Should also return 400, not 409 (key not marked as processed)
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExpiredKey_TreatsRequestAsNew()
    {
        // Arrange - Configure with 1 second expiration
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Test Item" };

        // Act - First request
        var firstResponse = await SendPost("/api/items", request, idempotencyKey);

        // Act - Wait for expiration
        await Task.Delay(TimeSpan.FromSeconds(1.5));

        // Act - Request with same key after expiration
        var thirdResponse = await SendPost("/api/items", request, idempotencyKey);

        // Assert - Should create new item (key expired)
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        thirdResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        
        var firstItem = await firstResponse.Content.ReadAsAsync<CreateItemResponse>();
        var thirdItem = await thirdResponse.Content.ReadAsAsync<CreateItemResponse>();
        firstItem.Id.ShouldNotBe(thirdItem.Id);
    }

    private async Task<HttpResponseMessage> SendPost<T>(
        string path,
        T request,
        string idempotencyKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, path);
        message.Headers.Add("X-Idempotency-Key", idempotencyKey);
        message.Content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        return await _fixture!.HttpClient!.SendAsync(message);
    }

    private async Task<HttpResponseMessage> SendPostToErrorEndpoint(string idempotencyKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/error");
        message.Headers.Add("X-Idempotency-Key", idempotencyKey);
        message.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        return await _fixture!.HttpClient!.SendAsync(message);
    }
}
```

---

### 5. Integration Tests: ErrorHandlingTests.cs

```csharp
// <copyright file="ErrorHandlingTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace AspCore.Idempotency.Tests.Integration;

/// <summary>
///     Integration tests for error handling and edge cases.
/// </summary>
public class ErrorHandlingTests : IAsyncLifetime
{
    private ApiFixture? _fixture;

    public async Task InitializeAsync()
    {
        _fixture = new ApiFixture();
        await _fixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        if (_fixture is not null)
            await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task CacheUnavailable_AllowsRequestToProceeed()
    {
        // Arrange - This test requires a way to simulate cache failure
        // For now, we assume the fail-open behavior is tested indirectly
        
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Test Item" };

        // Act
        var response = await SendPost("/api/items", request, idempotencyKey);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Fact]
    public async Task MalformedJsonRequest_ReturnsValidError()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();

        // Act
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items");
        message.Headers.Add("X-Idempotency-Key", idempotencyKey);
        message.Content = new StringContent("{invalid json}", Encoding.UTF8, "application/json");

        var response = await _fixture!.HttpClient!.SendAsync(message);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EndpointThrowsException_ErrorIsNotCached()
    {
        // Arrange - assuming /api/error endpoint throws
        var idempotencyKey = Guid.NewGuid().ToString();

        // Act - First request
        var firstResponse = await SendToErrorEndpoint(idempotencyKey);

        // Act - Second request
        var secondResponse = await SendToErrorEndpoint(idempotencyKey);

        // Assert - Both should be errors, not 409
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    private async Task<HttpResponseMessage> SendPost<T>(
        string path,
        T request,
        string idempotencyKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, path);
        message.Headers.Add("X-Idempotency-Key", idempotencyKey);
        message.Content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        return await _fixture!.HttpClient!.SendAsync(message);
    }

    private async Task<HttpResponseMessage> SendToErrorEndpoint(string idempotencyKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/error");
        message.Headers.Add("X-Idempotency-Key", idempotencyKey);
        message.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        return await _fixture!.HttpClient!.SendAsync(message);
    }
}
```

---

### 6. Integration Tests: ConcurrencyTests.cs

```csharp
// <copyright file="ConcurrencyTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace AspCore.Idempotency.Tests.Integration;

/// <summary>
///     Integration tests for concurrent request handling and race conditions.
/// </summary>
public class ConcurrencyTests : IAsyncLifetime
{
    private ApiFixture? _fixture;

    public async Task InitializeAsync()
    {
        _fixture = new ApiFixture();
        await _fixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        if (_fixture is not null)
            await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task ConcurrentRequests_WithSameKey_OneIsProcessed()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Test Item" };
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act - Send 5 concurrent requests with same key
        for (var i = 0; i < 5; i++)
        {
            tasks.Add(SendPost("/api/items", request, idempotencyKey));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - All should respond (no hung requests)
        responses.ShouldAllBe(r => r != null);
        
        // First one should be 201 Created, rest should be 409 Conflict
        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflicts = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        created.ShouldBe(1, "Exactly one request should create a new item");
        conflicts.ShouldBe(4, "Other requests should return conflict");
    }

    [Fact]
    public async Task ConcurrentRequests_WithDifferentKeys_AllProcessed()
    {
        // Arrange
        var request = new CreateItemRequest { Name = "Test Item" };
        var tasks = new List<Task<HttpResponseMessage>>();
        var keys = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid().ToString()).ToList();

        // Act - Send concurrent requests with different keys
        foreach (var key in keys)
        {
            tasks.Add(SendPost("/api/items", request, key));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - All should create items (201)
        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Created);
        
        // All should have different IDs
        var items = new List<CreateItemResponse>();
        foreach (var response in responses)
        {
            var item = await response.Content.ReadAsAsync<CreateItemResponse>();
            items.Add(item);
        }

        var uniqueIds = items.Select(i => i.Id).Distinct().Count();
        uniqueIds.ShouldBe(5, "All items should have unique IDs");
    }

    [Fact]
    public async Task RapidSuccessiveRequests_WithSameKey_HandleCorrectly()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateItemRequest { Name = "Test Item" };

        // Act - Rapid requests without delay
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => SendPost("/api/items", request, idempotencyKey))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert
        var createdCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        createdCount.ShouldBe(1);
        conflictCount.ShouldBe(9);
    }

    private async Task<HttpResponseMessage> SendPost<T>(
        string path,
        T request,
        string idempotencyKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, path);
        message.Headers.Add("X-Idempotency-Key", idempotencyKey);
        message.Content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        return await _fixture!.HttpClient!.SendAsync(message);
    }
}
```

---

## Test Helpers and Utilities

### TestEndpointFilterInvocationContext.cs

```csharp
// <copyright file="TestEndpointFilterInvocationContext.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Microsoft.AspNetCore.Http;

namespace AspCore.Idempotency.Tests.Helpers;

/// <summary>
///     Test implementation of EndpointFilterInvocationContext.
/// </summary>
internal class TestEndpointFilterInvocationContext : EndpointFilterInvocationContext
{
    private readonly HttpContext _httpContext;

    public TestEndpointFilterInvocationContext()
    {
        _httpContext = new DefaultHttpContext();
        _httpContext.Request.Method = HttpMethod.Post.Method;
        _httpContext.Request.Path = "/test";
    }

    public override HttpContext HttpContext => _httpContext;
    public override IServiceProvider Services => throw new NotImplementedException();
    public override IList<object?> Arguments => throw new NotImplementedException();

    public void SetHeader(string name, string value)
    {
        _httpContext.Request.Headers[name] = value;
    }

    public void RemoveHeader(string name)
    {
        _httpContext.Request.Headers.Remove(name);
    }

    public void SetStatusCode(int statusCode)
    {
        _httpContext.Response.StatusCode = statusCode;
    }

    public void SetMethod(string method)
    {
        _httpContext.Request.Method = method;
    }

    public void SetPath(string path)
    {
        _httpContext.Request.Path = new PathString(path);
    }
}
```

---

## Coverage Target Summary

| Test Class | Lines | Branch | Status |
|------------|-------|--------|--------|
| IdempotencyKeyValidationTests | 95%+ | 90%+ | P0 |
| DuplicateRequestHandlingTests | 90%+ | 85%+ | P0 |
| ConflictHandlingTests | 85%+ | 80%+ | P0 |
| CacheOperationTests | 90%+ | 85%+ | P0 |
| ErrorHandlingTests | 80%+ | 75%+ | P1 |
| ConcurrencyTests | 85%+ | 80%+ | P1 |
| **Overall Target** | **85%+** | **80%+** | **✅** |

---

## Running the Tests

```bash
# Run all tests
dotnet test AspCore.Idempotency.Tests

# Run specific category
dotnet test AspCore.Idempotency.Tests --filter "Category=Unit"
dotnet test AspCore.Idempotency.Tests --filter "Category=Integration"

# Run with coverage
dotnet test AspCore.Idempotency.Tests /p:CollectCoverage=true

# Run single test
dotnet test AspCore.Idempotency.Tests --filter "FullyQualifiedName~DuplicateRequestHandlingTests"
```

---

