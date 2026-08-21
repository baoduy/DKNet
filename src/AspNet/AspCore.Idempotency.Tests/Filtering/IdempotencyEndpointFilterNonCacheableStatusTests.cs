// <copyright file="IdempotencyEndpointFilterNonCacheableStatusTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Net;
using AspCore.Idempotency.Tests.Fixtures;

namespace AspCore.Idempotency.Tests.Filtering;

public class IdempotencyEndpointFilterNonCacheableStatusTests(ApiFixtureThrowConflict fixture)
    : IClassFixture<ApiFixtureThrowConflict>
{
    #region Methods

    [Fact]
    public async Task InvokeAsync_WhenResultStatusCodeIsNotCacheable_FirstRequestSucceedsWithoutCachingResponse()
    {
        // Arrange - handler result (404) falls outside the default 200-299 caching range, so
        // CacheResponseIfApplicableAsync's ShouldCacheStatusCode check skips caching the response body.
        var client = fixture.CreateClient();
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/rejects");
        request.Headers.Add("X-Idempotency-Key", idempotencyKey);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        ((int)response.StatusCode).ShouldBe((int)HttpStatusCode.NotFound);
    }

    #endregion
}
