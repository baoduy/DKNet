using System.Net;
using System.Net.Http.Json;
using AspCore.Extensions.Tests.Fixtures;
using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Responses;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Exercises the entity-oriented endpoint mappers (<c>MapGetById</c>, <c>MapGetList</c>) end to end through a
///     real ASP.NET Core minimal-API pipeline (TestServer) backed by a real <c>IRepositorySpec</c> / EF Core store —
///     verb, route, entity-to-model projection, status code, and published response shape. The delete overload and
///     the paging-hardening rules have their own suites (see <see cref="MapDeleteByIdTests" />,
///     <see cref="MapGetListPagingTests" />).
/// </summary>
public class FluentEntityEndpointMapperExtensionsTests(EndpointTestHost host) : IClassFixture<EndpointTestHost>
{
    #region Methods

    // --- GET by id: generic entity -> model projection, backed by a real IRepositorySpec / EF Core store ----

    [Fact]
    public async Task MapGetById_ExistingId_Returns200WithProjectedModel()
    {
        var response = await host.Client.GetAsync($"/t/widgets/{host.SeededWidgetId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var model = await response.Content.ReadFromJsonAsync<WidgetModel>();
        model.ShouldNotBeNull();
        model.Id.ShouldBe(host.SeededWidgetId);
        model.Name.ShouldBe("second");
    }

    [Fact]
    public async Task MapGetById_UnknownId_Returns404()
    {
        var response = await host.Client.GetAsync($"/t/widgets/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // --- GET list: generic entity -> model page, newest-first ordering ---------------------------------------

    [Fact]
    public async Task MapGetList_Returns200WithAllSeededWidgets_NewestIdFirst()
    {
        var response = await host.Client.GetAsync("/t/widgets?pageNumber=1&pageSize=10");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(3);
        page.Items.Select(i => i.Name).ShouldBe(["third", "second", "first"]);
    }

    // --- Published API description: the Produces<> type/status actually advertised on endpoint metadata -----
    // Reverting the Produces<>() call to a wrong response type leaves the runtime-body assertions above green —
    // a client generator reading the endpoint description would still be wrong. This asserts the metadata
    // directly, so it fails the moment the corresponding Produces<>() call changes.

    [Fact]
    public void MapGetList_DeclaresPagedResponseAsThe200ResponseType()
    {
        var producesType = GetSuccessProducesType("/t/widgets", HttpStatusCode.OK);

        producesType.ShouldBe(typeof(PagedResponse<WidgetModel>));
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
