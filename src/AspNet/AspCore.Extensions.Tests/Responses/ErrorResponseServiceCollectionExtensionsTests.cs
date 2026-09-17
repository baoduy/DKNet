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
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Extensions.Tests.Responses;

public class ErrorResponseServiceCollectionExtensionsTests
{
    #region Methods

    [Fact]
    public void AddErrorResponses_CalledTwice_SecondCallIsANoOp()
    {
        // REWORK round 1 (DRK-1490, nit): AddSingleton let a second call register a second ErrorResponseOptions
        // and a second IStartupFilter, inserting UseExceptionHandler() twice. TryAddSingleton/TryAddEnumerable
        // make the second call a no-op: exactly one of each survives, and the first call's setting wins.
        var services = new ServiceCollection();

        services.AddErrorResponses(o => o.StatusCode = _ => 422);
        services.AddErrorResponses(o => o.StatusCode = _ => 599);

        services.Count(d => d.ServiceType == typeof(ErrorResponseOptions)).ShouldBe(1);
        services.Count(d => d.ServiceType == typeof(IStartupFilter)).ShouldBe(1);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ErrorResponseOptions>();
        options.StatusCode!.Invoke(null!).ShouldBe(422);
    }

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
