// <copyright file="IdempotencyEndpointFilterCasingTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Net;
using System.Text;
using System.Text.Json;
using AspCore.Idempotency.Tests.Fixtures;

namespace AspCore.Idempotency.Tests.Filtering;

public class IdempotencyEndpointFilterCasingTests(ApiFixtureThrowConflict fixture)
    : IClassFixture<ApiFixtureThrowConflict>
{
    #region Methods

    [Fact]
    public async Task InvokeAsync_WhenSamePathDiffersOnlyByCasing_TreatsSecondRequestAsDuplicate()
    {
        // Arrange
        var client = fixture.CreateClient();
        var idempotencyKey = Guid.NewGuid().ToString();
        var json = JsonSerializer.Serialize(new { name = "test item" });

        var content1 = new StringContent(json, Encoding.UTF8, "application/json");
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = content1 };
        request1.Headers.Add("X-Idempotency-Key", idempotencyKey);

        var content2 = new StringContent(json, Encoding.UTF8, "application/json");
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/Items") { Content = content2 };
        request2.Headers.Add("X-Idempotency-Key", idempotencyKey);

        // Act - first request against lowercase path
        var response1 = await client.SendAsync(request1);

        // Act - second request, same key, path differs only by casing
        var response2 = await client.SendAsync(request2);

        // Assert - same route scope regardless of request path casing
        ((int)response1.StatusCode).ShouldBe((int)HttpStatusCode.Created);
        ((int)response2.StatusCode).ShouldBe((int)HttpStatusCode.Conflict);
    }

    #endregion
}
