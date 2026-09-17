// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ErrorResponseServiceCollectionExtensionsTests.cs
// Description: DRK-1484 R9 (Build-stage addition, not in the frozen AT set) — proves AddErrorResponses alone
// wires an unhandled exception through to the standard body for an endpoint the FluentsEndpointMapperExtensions
// helpers do NOT wrap (i.e. exercises the registered IExceptionHandler + UseExceptionHandler() end to end,
// rather than the mapped-command catch UnifiedErrorResponseEndpointTests exercises). Runs in Production so
// ASP.NET Core's own Development-only exception page — which sits closer to the endpoint than any
// IExceptionHandler — does not intercept first.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DKNet.AspCore.Extensions.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace AspCore.Extensions.Tests.Responses;

public class ErrorResponseServiceCollectionExtensionsTests
{
    #region Methods

    [Fact]
    public async Task AddErrorResponses_RawEndpointThrows_HostCallsOnlyAddErrorResponses_AnswersStandardBody()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = "Production";
        builder.WebHost.UseTestServer();
        builder.Services.AddErrorResponses();

        await using var app = builder.Build();
        app.MapGet("/boom", (Microsoft.AspNetCore.Http.HttpContext _) => throw new InvalidOperationException("secret"));

        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/boom");

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().ShouldBe("Error");
        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        errors.ShouldHaveSingleItem();
        errors[0].GetProperty("message").GetString()!.ShouldNotContain("secret");
    }

    #endregion
}
