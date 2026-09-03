// <copyright file="IdempotencyEndpointFilterValueExtractionTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Net;
using DKNet.AspCore.Idempotency;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Idempotency.Tests.Filtering;

/// <summary>
///     Proves <c>CacheResponseIfApplicableAsync</c>'s value-extraction switched from reflecting a "Value" property
///     to the declared <see cref="IValueHttpResult" /> contract without changing observable behaviour: a
///     <see cref="TypedResults.Ok{TValue}" /> result caches its <c>Value</c>, and a handler result that is not an
///     <see cref="IValueHttpResult" /> at all still caches the result itself.
/// </summary>
public sealed class IdempotencyEndpointFilterValueExtractionTests
{
    #region Methods

    private static async Task<(WebApplication app, HttpClient client)> StartHostAsync(
        Action<IEndpointRouteBuilder> mapEndpoints)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddIdempotentKey(
            o => o.ConflictHandling = IdempotentConflictHandling.CachedResult);

        var app = builder.Build();
        mapEndpoints(app);
        await app.StartAsync();

        return (app, app.GetTestClient());
    }

    private static HttpRequestMessage NewRequest(string path, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-Idempotency-Key", idempotencyKey);
        return request;
    }

    [Fact]
    public async Task InvokeAsync_WhenResultIsTypedResultsOk_CachesValueFromIValueHttpResult()
    {
        // Arrange - TypedResults.Ok<T> implements IValueHttpResult; the filter must extract v.Value.
        var (app, client) = await StartHostAsync(endpoints =>
            endpoints.MapPost("/ok", () => TypedResults.Ok(new { name = "widget" }))
                .RequiredIdempotentKey());
        var key = Guid.NewGuid().ToString();

        var response1 = await client.SendAsync(NewRequest("/ok", key));
        var body1 = await response1.Content.ReadAsStringAsync();
        var response2 = await client.SendAsync(NewRequest("/ok", key));
        var body2 = await response2.Content.ReadAsStringAsync();

        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        body1.ShouldContain("widget");
        body2.ShouldBe(body1);
        await app.StopAsync();
    }

    [Fact]
    public async Task InvokeAsync_WhenResultIsNotIValueHttpResult_CachesTheResultItself()
    {
        // Arrange - a handler returning a plain object (not wrapped in any IResult) reaches the filter as that
        // object directly; it is not an IValueHttpResult, so the fallback must cache the object itself.
        var (app, client) = await StartHostAsync(endpoints =>
            endpoints.MapPost("/plain", () => new { name = "gadget" })
                .RequiredIdempotentKey());
        var key = Guid.NewGuid().ToString();

        var response1 = await client.SendAsync(NewRequest("/plain", key));
        var body1 = await response1.Content.ReadAsStringAsync();
        var response2 = await client.SendAsync(NewRequest("/plain", key));
        var body2 = await response2.Content.ReadAsStringAsync();

        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        body1.ShouldContain("gadget");
        body2.ShouldBe(body1);
        await app.StopAsync();
    }

    #endregion
}
