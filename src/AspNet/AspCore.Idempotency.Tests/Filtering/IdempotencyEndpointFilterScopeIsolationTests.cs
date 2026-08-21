// <copyright file="IdempotencyEndpointFilterScopeIsolationTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Net;
using System.Text;
using System.Text.Json;
using AspCore.Idempotency.Tests.Fixtures;

namespace AspCore.Idempotency.Tests.Filtering;

public class IdempotencyEndpointFilterScopeIsolationTests(ApiFixtureThrowConflict fixture)
    : IClassFixture<ApiFixtureThrowConflict>
{
    #region Methods

    [Fact]
    public async Task TwoAuthenticatedPrincipals_SameIdempotencyKey_EachGetOwnCreated()
    {
        // Arrange
        var client = fixture.HttpClient!;
        var sharedKey = Guid.NewGuid().ToString();

        // Act
        var responseA = await SendAsync(client, sharedKey, "user-a", "item A");
        var responseB = await SendAsync(client, sharedKey, "user-b", "item B");

        // Assert
        ((int)responseA.StatusCode).ShouldBe((int)HttpStatusCode.Created);
        ((int)responseB.StatusCode).ShouldBe((int)HttpStatusCode.Created);
    }

    #endregion

    #region Helpers

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string idempotencyKey,
        string userId,
        string name)
    {
        var json = JsonSerializer.Serialize(new { name });
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = content };
        request.Headers.Add("X-Idempotency-Key", idempotencyKey);
        request.Headers.Add("X-Test-UserId", userId);
        return await client.SendAsync(request);
    }

    #endregion
}
