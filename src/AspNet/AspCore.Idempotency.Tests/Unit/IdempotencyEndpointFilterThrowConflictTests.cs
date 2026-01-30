// <copyright file="IdempotencyEndpointFilterTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Net;
using System.Text;
using System.Text.Json;
using AspCore.Idempotency.Tests.Fixtures;

namespace AspCore.Idempotency.Tests.Unit;

public class IdempotencyEndpointFilterThrowConflictTests(ApiFixtureThrowConflict fixture)
    : IClassFixture<ApiFixtureThrowConflict>
{
    #region Methods

    [Fact]
    public async Task InvokeAsync_WhenDifferentKeysUsed_ReturnsMultipleResults()
    {
        // Arrange
        var client = fixture.HttpClient!;
        var key1 = Guid.NewGuid().ToString();
        var key2 = Guid.NewGuid().ToString();

        var json1 = JsonSerializer.Serialize(new { name = "item 1" });
        var content1 = new StringContent(json1, Encoding.UTF8, "application/json");
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Content = content1
        };
        request1.Headers.Add("X-Idempotency-Key", key1);

        var json2 = JsonSerializer.Serialize(new { name = "item 2" });
        var content2 = new StringContent(json2, Encoding.UTF8, "application/json");
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Content = content2
        };
        request2.Headers.Add("X-Idempotency-Key", key2);

        // Act
        var response1 = await client.SendAsync(request1);
        var contentBody1 = await response1.Content.ReadAsStringAsync();
        var response2 = await client.SendAsync(request2);
        var contentBody2 = await response2.Content.ReadAsStringAsync();

        // Assert
        ((int)response1.StatusCode).ShouldBe((int)HttpStatusCode.Created);
        ((int)response2.StatusCode).ShouldBe((int)HttpStatusCode.Created);
        contentBody1.ShouldNotBe(contentBody2);
    }

    [Fact]
    public async Task InvokeAsync_WhenEndpointNotRequiringIdempotency_AllowsRequestWithoutKey()
    {
        // Arrange
        var client = fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/api/health");

        // Assert
        ((int)response.StatusCode).ShouldBe((int)HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvokeAsync_WhenIdempotencyKeyHeaderEmpty_Returns400BadRequest()
    {
        // Arrange
        var client = fixture.CreateClient();
        var json = JsonSerializer.Serialize(new { name = "test item" });
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Content = content
        };
        request.Headers.Add("X-Idempotency-Key", "");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        ((int)response.StatusCode).ShouldBe((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_WhenIdempotencyKeyHeaderMissing_Returns400BadRequest()
    {
        // Arrange
        var client = fixture.CreateClient();
        var json = JsonSerializer.Serialize(new { name = "test item" });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/items", content);

        // Assert
        ((int)response.StatusCode).ShouldBe((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_WhenKeyIsDuplicated_Returns409Conflict()
    {
        // Arrange
        var client = fixture.CreateClient();
        var idempotencyKey = Guid.NewGuid().ToString();
        var json = JsonSerializer.Serialize(new { name = "test item" });
        var content1 = new StringContent(json, Encoding.UTF8, "application/json");
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Content = content1
        };
        request1.Headers.Add("X-Idempotency-Key", idempotencyKey);

        // Act - First request
        var response1 = await client.SendAsync(request1);

        // Act - Second request with same key
        var content2 = new StringContent(json, Encoding.UTF8, "application/json");
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Content = content2
        };
        request2.Headers.Add("X-Idempotency-Key", idempotencyKey);
        var response2 = await client.SendAsync(request2);

        // Assert
        ((int)response1.StatusCode).ShouldBe((int)HttpStatusCode.Created);
        ((int)response2.StatusCode).ShouldBe((int)HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InvokeAsync_WhenKeyIsNew_Returns201Created()
    {
        // Arrange
        var client = fixture.CreateClient();
        var idempotencyKey = Guid.NewGuid().ToString();
        var json = JsonSerializer.Serialize(new { name = "test item" });
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Content = content
        };
        request.Headers.Add("X-Idempotency-Key", idempotencyKey);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        ((int)response.StatusCode).ShouldBe((int)HttpStatusCode.Created);
    }

    [Fact]
    public async Task InvokeAsync_WithValidIdempotencyKey_IncludesResponseHeaders()
    {
        // Arrange
        var client = fixture.CreateClient();
        var idempotencyKey = Guid.NewGuid().ToString();
        var json = JsonSerializer.Serialize(new { name = "test item" });
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Content = content
        };
        request.Headers.Add("X-Idempotency-Key", idempotencyKey);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        ((int)response.StatusCode).ShouldBe((int)HttpStatusCode.Created);
        response.Headers.ShouldNotBeNull();
    }

    #endregion
}