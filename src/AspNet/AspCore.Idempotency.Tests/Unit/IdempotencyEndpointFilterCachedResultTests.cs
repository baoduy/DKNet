// <copyright file="IdempotencyEndpointFilterCachedResultTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Net;
using System.Text;
using System.Text.Json;
using AspCore.Idempotency.Tests.Fixtures;

namespace AspCore.Idempotency.Tests.Unit;

public class IdempotencyEndpointFilterCachedResultTests(ApiFixtureCachedResult fixture)
    : IClassFixture<ApiFixtureCachedResult>
{
    #region Methods

    [Fact]
    public async Task InvokeAsync_WhenKeyIsDuplicated_ReturnsCachedResponse()
    {
        // Arrange
        var client = fixture.HttpClient!;
        var idempotencyKey = Guid.NewGuid().ToString();
        var json = JsonSerializer.Serialize(new { name = "test item" });

        var content1 = new StringContent(json, Encoding.UTF8, "application/json");
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Content = content1
        };
        request1.Headers.Add("X-Idempotency-Key", idempotencyKey);

        // Act - first request
        var response1 = await client.SendAsync(request1);
        var body1 = await response1.Content.ReadAsStringAsync();

        // Act - second request with same key
        var content2 = new StringContent(json, Encoding.UTF8, "application/json");
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Content = content2
        };
        request2.Headers.Add("X-Idempotency-Key", idempotencyKey);

        var response2 = await client.SendAsync(request2);
        var body2 = await response2.Content.ReadAsStringAsync();

        // Assert
        ((int)response1.StatusCode).ShouldBe((int)HttpStatusCode.Created);
        ((int)response2.StatusCode).ShouldBe((int)HttpStatusCode.Created);
        body1.ShouldBe(body2);
    }

    #endregion
}