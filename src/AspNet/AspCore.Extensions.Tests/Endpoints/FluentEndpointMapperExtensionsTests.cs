using System.Net;
using System.Net.Http.Json;
using AspCore.Extensions.Tests.Fixtures;
using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Exercises the SlimMessageBus command/query endpoint mappers end to end through a real ASP.NET Core
///     minimal-API pipeline (TestServer) with a real in-memory SlimMessageBus and real handlers — verb, route,
///     dispatch, status code, and response shape — rather than asserting the mapper merely returned a non-null
///     <see cref="RouteHandlerBuilder" />. The entity-oriented mappers have their own suite (see
///     <see cref="FluentEntityEndpointMapperExtensionsTests" />).
/// </summary>
public class FluentEndpointMapperExtensionsTests(EndpointTestHost host) : IClassFixture<EndpointTestHost>
{
    #region Methods

    // --- POST: naming-derived created rule -----------------------------------------------------------------

    [Fact]
    public async Task MapPost_WithResponse_CreateNamedCommand_Returns201WithBody()
    {
        var response = await host.Client.PostAsJsonAsync(
            "/t/post-with-response-create", new CreateWidgetCommand { Name = "widget-1" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("widget-1");
    }

    [Fact]
    public async Task MapPost_WithResponse_NonCreateNamedCommand_Returns200WithBody()
    {
        var response = await host.Client.PostAsJsonAsync(
            "/t/post-with-response-update", new RenameWidgetCommand { Name = "widget-2" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("widget-2");
    }

    [Fact]
    public async Task MapPost_NoResponse_CreateNamedCommand_Returns201()
    {
        var response = await host.Client.PostAsJsonAsync("/t/post-no-response-create", new CreateNoResponseCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task MapPost_NoResponse_NonCreateNamedCommand_Returns200()
    {
        var response = await host.Client.PostAsJsonAsync("/t/post-no-response-update", new EchoNoResponseCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // --- Shared error responses (ProblemDetails on failure) -------------------------------------------------

    [Fact]
    public async Task MapPost_WithResponse_FailedResult_Returns400WithProblemDetailsBody()
    {
        var response = await host.Client.PostAsJsonAsync(
            "/t/post-fail-with-response", new FailingWidgetCommand { Reason = "no-can-do" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.ShouldNotBeNull();
        problem.Detail.ShouldBe("no-can-do");
    }

    [Fact]
    public async Task MapPost_NoResponse_FailedResult_Returns400WithProblemDetailsBody()
    {
        var response = await host.Client.PostAsJsonAsync("/t/post-fail-no-response", new FailingNoResponseCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.ShouldNotBeNull();
        problem.Detail.ShouldBe("no-response-boom");
    }

    // --- DELETE (no-response overload; see MapDeleteWithResponseTests for the "with response" overload) ----

    [Fact]
    public async Task MapDelete_NoResponse_Returns200()
    {
        var response = await host.Client.DeleteAsync("/t/delete-no-response");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // --- PATCH ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task MapPatch_NoResponse_Returns200()
    {
        var response = await host.Client.PatchAsync(
            "/t/patch-no-response", JsonContent.Create(new EchoNoResponseCommand()));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MapPatch_WithResponse_Returns200WithBody()
    {
        var response = await host.Client.PatchAsync(
            "/t/patch-with-response", JsonContent.Create(new RenameWidgetCommand { Name = "patched" }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("patched");
    }

    // --- PUT --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task MapPut_NoResponse_Returns200()
    {
        var response = await host.Client.PutAsJsonAsync("/t/put-no-response", new EchoNoResponseCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MapPut_WithResponse_Returns200WithBody()
    {
        var response = await host.Client.PutAsJsonAsync(
            "/t/put-with-response", new RenameWidgetCommand { Name = "put-name" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("put-name");
    }

    // --- GET: query dispatch + missing-resource (404) handling ----------------------------------------------

    [Fact]
    public async Task MapGet_Query_Found_Returns200WithBody()
    {
        var response = await host.Client.GetAsync("/t/get-query?Found=true&Name=present");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("present");
    }

    [Fact]
    public async Task MapGet_Query_NotFound_Returns404()
    {
        var response = await host.Client.GetAsync("/t/get-query?Found=false&Name=absent");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // --- GET: paged query -------------------------------------------------------------------------------------

    [Fact]
    public async Task MapGetPage_Query_Returns200WithPagedResponseShape()
    {
        var response = await host.Client.GetAsync("/t/get-page");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetResult>>();
        page.ShouldNotBeNull();
        page.PageNumber.ShouldBe(1);
        page.PageSize.ShouldBe(2);
        page.TotalItemCount.ShouldBe(5);
        page.Items.Select(i => i.Name).ShouldBe(["a", "b"]);
    }

    // --- ProducesCommons: shared error status codes are advertised on endpoint metadata ---------------------

    [Fact]
    public async Task ProducesCommons_AddsCommonErrorStatusCodeMetadata()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.MapGroup("/x").MapGet("/y", () => "ok").ProducesCommons();
        await app.StartAsync();

        var endpoint = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/x/y");
        var statusCodes = endpoint.Metadata
            .OfType<IProducesResponseTypeMetadata>()
            .Select(m => m.StatusCode)
            .ToHashSet();

        await app.StopAsync();

        // Assert containment, not exact-set equality — the endpoint also carries its own inferred 200 success
        // metadata (from the () => "ok" delegate), which is not ProducesCommons' concern.
        foreach (var expected in new[]
                 {
                     StatusCodes.Status400BadRequest,
                     StatusCodes.Status401Unauthorized,
                     StatusCodes.Status403Forbidden,
                     StatusCodes.Status404NotFound,
                     StatusCodes.Status409Conflict,
                     StatusCodes.Status429TooManyRequests,
                     StatusCodes.Status500InternalServerError
                 })
            statusCodes.ShouldContain(expected);
    }

    // --- Published API description: the Produces<> type/status actually advertised on endpoint metadata -----
    // Reverting a Produces<>() call to a wrong response type/status leaves the runtime-body assertions above
    // green — a client generator reading the endpoint description would still be wrong. These assert the
    // metadata directly, so they fail the moment the corresponding Produces<>() call changes.

    [Fact]
    public void MapGetPage_DeclaresPagedResponseAsThe200ResponseType()
    {
        var producesType = GetSuccessProducesType("/t/get-page", HttpStatusCode.OK);

        producesType.ShouldBe(typeof(PagedResponse<WidgetResult>));
    }

    [Fact]
    public void MapPost_WithResponse_CreateNamedCommand_Declares201AsTheProducesStatusCode()
    {
        var producesType = GetSuccessProducesType("/t/post-with-response-create", HttpStatusCode.Created);

        producesType.ShouldBe(typeof(WidgetResult));
    }

    [Fact]
    public void MapPost_WithResponse_NonCreateNamedCommand_Declares200AsTheProducesStatusCode()
    {
        var producesType = GetSuccessProducesType("/t/post-with-response-update", HttpStatusCode.OK);

        producesType.ShouldBe(typeof(WidgetResult));
    }

    private Type? GetSuccessProducesType(string routeRawText, HttpStatusCode expectedStatusCode)
    {
        var endpoint = host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == routeRawText);
        return endpoint.Metadata
            .OfType<IProducesResponseTypeMetadata>()
            .Single(m => m.StatusCode == (int)expectedStatusCode)
            .Type;
    }

    #endregion
}
