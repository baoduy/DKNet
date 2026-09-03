// <copyright file="IdempotencyGroupCoverageTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AspCore.Idempotency.Tests.Fixtures;

namespace AspCore.Idempotency.Tests.Filtering;

public class IdempotencyGroupCoverageTests(ApiFixtureThrowConflict fixture) : IClassFixture<ApiFixtureThrowConflict>
{
    #region Methods

    private static StringContent ItemContent(string name = "test item") =>
        new(JsonSerializer.Serialize(new { name }), Encoding.UTF8, "application/json");

    private static HttpRequestMessage WithKey(HttpMethod method, string url, string key, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add("X-Idempotency-Key", key);
        return request;
    }

    [Fact]
    public async Task InvokeAsync_WhenGroupOnlyEndpointCalledWithoutKey_Returns400BadRequest()
    {
        // Arrange
        var client = fixture.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders/", ItemContent());

        // Assert
        ((int)response.StatusCode).ShouldBe((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_WhenGroupOnlyEndpointCalledWithSameKeyTwice_SecondReturns409Conflict()
    {
        // Arrange
        var client = fixture.CreateClient();
        var key = Guid.NewGuid().ToString();

        // Act
        var response1 = await client.SendAsync(WithKey(HttpMethod.Post, "/api/orders/", key, ItemContent()));
        var response2 = await client.SendAsync(WithKey(HttpMethod.Post, "/api/orders/", key, ItemContent()));

        // Assert
        ((int)response1.StatusCode).ShouldBe((int)HttpStatusCode.Created);
        ((int)response2.StatusCode).ShouldBe((int)HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InvokeAsync_WhenReadAndUncoveredVerbEndpointsCalledWithoutKey_AllReturn200OK()
    {
        // Arrange
        var client = fixture.CreateClient();
        var id = Guid.NewGuid().ToString();

        // Act
        var list = await client.GetAsync("/api/orders/");
        var fetch = await client.GetAsync($"/api/orders/{id}");
        var update = await client.PutAsync($"/api/orders/{id}", ItemContent());

        // Assert
        ((int)list.StatusCode).ShouldBe((int)HttpStatusCode.OK);
        ((int)fetch.StatusCode).ShouldBe((int)HttpStatusCode.OK);
        ((int)update.StatusCode).ShouldBe((int)HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvokeAsync_WhenUncoveredCancelEndpointCalledWithSameKeyTwice_ProcessesBothRequests()
    {
        // Arrange
        var client = fixture.CreateClient();
        var id = Guid.NewGuid().ToString();
        var key = Guid.NewGuid().ToString();

        // Act
        var response1 = await client.SendAsync(WithKey(HttpMethod.Delete, $"/api/orders/{id}", key));
        var response2 = await client.SendAsync(WithKey(HttpMethod.Delete, $"/api/orders/{id}", key));
        var body2 = await response2.Content.ReadFromJsonAsync<JsonElement>();

        // Assert - the second call must re-run the side effect, not be reported as a duplicate
        ((int)response1.StatusCode).ShouldBe((int)HttpStatusCode.OK);
        ((int)response2.StatusCode).ShouldBe((int)HttpStatusCode.OK);
        body2.GetProperty("cancelCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task InvokeAsync_WhenMethodOverrideHeaderSentWithoutKey_StillReturns400BadRequest()
    {
        // Arrange
        var client = fixture.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders/") { Content = ItemContent() };
        request.Headers.Add("X-HTTP-Method-Override", "GET");

        // Act
        var response = await client.SendAsync(request);

        // Assert - coverage comes from the routed POST metadata, never from the override header
        ((int)response.StatusCode).ShouldBe((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_WhenDoublyDeclaredEndpointCalledWithNewKey_FirstRequestSucceeds()
    {
        // Arrange
        var client = fixture.CreateClient();
        var key = Guid.NewGuid().ToString();

        // Act
        var response = await client.SendAsync(WithKey(HttpMethod.Post, "/api/orders/checkout", key, ItemContent()));

        // Assert - a naive double-reservation would answer this very first request as a duplicate
        ((int)response.StatusCode).ShouldBe((int)HttpStatusCode.Created);
    }

    [Fact]
    public async Task InvokeAsync_WhenDoublyDeclaredEndpointCalledWithSameKeyTwice_SecondReturns409ConflictOnce()
    {
        // Arrange
        var client = fixture.CreateClient();
        var key = Guid.NewGuid().ToString();

        // Act
        var response1 = await client.SendAsync(WithKey(HttpMethod.Post, "/api/orders/checkout", key, ItemContent()));
        var response2 = await client.SendAsync(WithKey(HttpMethod.Post, "/api/orders/checkout", key, ItemContent()));

        // Assert
        ((int)response1.StatusCode).ShouldBe((int)HttpStatusCode.Created);
        ((int)response2.StatusCode).ShouldBe((int)HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InvokeAsync_WhenNestedRefundGroupCalledWithSameKeyTwice_SecondReturns409Conflict()
    {
        // Arrange
        var client = fixture.CreateClient();
        var id = Guid.NewGuid().ToString();
        var key = Guid.NewGuid().ToString();

        // Act
        var response1 = await client.SendAsync(WithKey(HttpMethod.Post, $"/api/orders/{id}/refunds", key, ItemContent()));
        var response2 = await client.SendAsync(WithKey(HttpMethod.Post, $"/api/orders/{id}/refunds", key, ItemContent()));

        // Assert - nested group inherits the outer group's coverage
        ((int)response1.StatusCode).ShouldBe((int)HttpStatusCode.Created);
        ((int)response2.StatusCode).ShouldBe((int)HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InvokeAsync_WhenLaterAddedEndpointCalledWithoutKey_Returns400BadRequest()
    {
        // Arrange
        var client = fixture.CreateClient();
        var id = Guid.NewGuid().ToString();

        // Act - endpoint declares nothing of its own, coverage must still come from the group
        var response = await client.PostAsync($"/api/orders/{id}/notes", ItemContent());

        // Assert
        ((int)response.StatusCode).ShouldBe((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_WhenExplicitVerbSetGroupPostAndDeleteCalledWithSameKeyTwice_BothReturn409ConflictOnRepeat()
    {
        // Arrange
        var client = fixture.CreateClient();
        var postKey = Guid.NewGuid().ToString();
        var deleteKey = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid().ToString();

        // Act
        var post1 = await client.SendAsync(WithKey(HttpMethod.Post, "/api/admin/tenants", postKey, ItemContent()));
        var post2 = await client.SendAsync(WithKey(HttpMethod.Post, "/api/admin/tenants", postKey, ItemContent()));
        var delete1 = await client.SendAsync(WithKey(HttpMethod.Delete, $"/api/admin/tenants/{tenantId}", deleteKey));
        var delete2 = await client.SendAsync(WithKey(HttpMethod.Delete, $"/api/admin/tenants/{tenantId}", deleteKey));

        // Assert - both named verbs are covered by the explicit set
        ((int)post1.StatusCode).ShouldBe((int)HttpStatusCode.Created);
        ((int)post2.StatusCode).ShouldBe((int)HttpStatusCode.Conflict);
        ((int)delete1.StatusCode).ShouldBe((int)HttpStatusCode.OK);
        ((int)delete2.StatusCode).ShouldBe((int)HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InvokeAsync_WhenExplicitVerbSetGroupPutCalledWithoutKey_Returns200OK()
    {
        // Arrange
        var client = fixture.CreateClient();
        var tenantId = Guid.NewGuid().ToString();

        // Act - PUT was not named in the group's verb set, so it stays unprotected
        var response = await client.PutAsync($"/api/admin/tenants/{tenantId}", ItemContent());

        // Assert
        ((int)response.StatusCode).ShouldBe((int)HttpStatusCode.OK);
    }

    #endregion
}
