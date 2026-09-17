// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: UnhandledExceptionEndpointFilterTests.cs
// Description: REWORK round 1 (DRK-1490, blocking finding) — the standard unhandled-error body must reach a
// query endpoint (MapGet<>/MapGetPage<>), not only the eleven command mappers. The endpoint filter
// FluentsEndpointMapperExtensions.ProducesCommons() now registers once catches at every mapper that calls it,
// queries included, and sits closer to the endpoint than ASP.NET Core's Development-only exception page. Pins
// the query/command equivalence rather than assuming it.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.Responses;
using DKNet.SlimBus.Extensions;
using FluentResults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace AspCore.Extensions.Tests.Endpoints;

public sealed record ExplodingQuery : Fluents.Queries.IWitResponse<string>;

internal sealed class ExplodingQueryHandler : Fluents.Queries.IHandler<ExplodingQuery, string>
{
    public Task<string?> OnHandle(ExplodingQuery request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("query-boom");
}

public sealed record ExplodingCommand : Fluents.Requests.INoResponse;

internal sealed class ExplodingCommandHandler : Fluents.Requests.IHandler<ExplodingCommand>
{
    public Task<IResultBase> OnHandle(ExplodingCommand request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("command-boom");
}

public class UnhandledExceptionEndpointFilterTests
{
    #region Methods

    private static async Task<(WebApplication App, HttpClient Client)> CreateHostAsync(string environmentName = "Development")
    {
        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = environmentName;
        builder.WebHost.UseTestServer();
        builder.Services.AddErrorResponses();
        builder.Services.AddSlimMessageBus(mbb => mbb
            .AddJsonSerializer()
            .AddServicesFromAssembly(typeof(UnhandledExceptionEndpointFilterTests).Assembly)
            .AddChildBus(
                "Memory",
                mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(UnhandledExceptionEndpointFilterTests).Assembly)));

        var app = builder.Build();
        var group = app.MapGroup("/x");
        group.MapGet<ExplodingQuery, string>("/query");
        group.MapPost<ExplodingCommand>("/command");

        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private static async Task AssertStandardUnhandledShapeAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().ShouldBe("Error");
        body.GetProperty("type").GetString().ShouldBe(HttpStatusCode.InternalServerError.ToString());
        body.TryGetProperty("traceId", out var traceId).ShouldBeTrue("body must carry a trace identifier");
        traceId.GetString().ShouldNotBeNullOrEmpty();
        body.TryGetProperty("detail", out _).ShouldBeFalse("the standard body carries no Detail member");
        body.TryGetProperty("exception", out _)
            .ShouldBeFalse("the standard body never carries the developer-exception-page's own exception member");

        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        errors.ShouldHaveSingleItem();
        errors[0].TryGetProperty("message", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task UnhandledException_ThroughMapGet_Development_AnswersStandardShape()
    {
        var (app, client) = await CreateHostAsync();
        await using var _ = app;

        var response = await client.GetAsync("/x/query");

        await AssertStandardUnhandledShapeAsync(response);
    }

    [Fact]
    public async Task UnhandledException_ThroughMapPost_Development_AnswersStandardShape()
    {
        // The command-mapper equivalent of the query test above — pins that MapGet<> and a command overload
        // answer identically, rather than assuming it from the shared endpoint filter.
        var (app, client) = await CreateHostAsync();
        await using var _ = app;

        var response = await client.PostAsJsonAsync("/x/command", new ExplodingCommand());

        await AssertStandardUnhandledShapeAsync(response);
    }

    [Fact]
    public async Task UnhandledException_Development_MessageIsTheExceptionsOwnMessage()
    {
        // REWORK round 2 (DRK-1490): pins the message content per environment, built the same way
        // Fixtures/LedgerTestHost.cs builds its host (builder.Environment.EnvironmentName, not a DI stub) — so
        // deleting either environment argument here (or the sibling Production test) turns a test red.
        var (app, client) = await CreateHostAsync("Development");
        await using var _ = app;

        var response = await client.GetAsync("/x/query");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var message = body.GetProperty("errors")[0].GetProperty("message").GetString();
        message.ShouldBe("query-boom");
    }

    [Fact]
    public async Task UnhandledException_Production_MessageIsTheFixedMessage()
    {
        var (app, client) = await CreateHostAsync("Production");
        await using var _ = app;

        var response = await client.GetAsync("/x/query");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var message = body.GetProperty("errors")[0].GetProperty("message").GetString();
        message.ShouldBe("An unexpected error occurred. Quote the trace-id when reporting this.");
    }

    #endregion
}
