using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DKNet.AspCore.Extensions.Responses;
using Shouldly;

namespace SlimBus.Generators.Tests.Api;

/// <summary>
///     End-to-end proof of the generated Gadget CRUD slice over a real HTTP host: every request record,
///     handler, and endpoint mapping exercised here is produced by <c>DKNet.SlimBus.Generators</c> from
///     <c>[CrudCreate]</c>/<c>[CrudUpdate]</c> on <see cref="SlimBus.Generators.Tests.Domain.Catalog.Gadget" />
///     — no hand-written request, handler, or endpoint code exists for this slice.
/// </summary>
public sealed class GadgetCrudSliceTests(GadgetTestHost host) : IClassFixture<GadgetTestHost>
{
    [Fact]
    public async Task PostGadget_WithValidBody_Returns201AndDtoBody()
    {
        var response = await host.Client.PostAsJsonAsync("/gadgets", new { name = "g", price = 5m });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        var dto = await response.Content.ReadFromJsonAsync<GadgetDto>();
        dto.ShouldNotBeNull();
        dto.Name.ShouldBe("g");
        dto.Price.ShouldBe(5m);
    }

    // ponytail: the repo's only established minimal-API validation pattern (SharpGrip
    // FluentValidation.AutoValidation + a hand-written AbstractValidator<T>) needs a hand-written validator
    // per request, which would break this task's zero-hand-written-code proof; .NET 10 minimal APIs have no
    // built-in DataAnnotations auto-validation either. TODO(DKCRUDGEN): wire real 400 coverage once a
    // generator-emitted validation story exists — see task-9-report.md for the full tradeoff.
    // [Fact] public async Task PostGadget_WithMissingName_Returns400()

    [Fact]
    public async Task PutGadgetPrice_WithExistingId_Returns200AndUpdatedDto()
    {
        var created = await host.Client.PostAsJsonAsync("/gadgets", new { name = "widget", price = 1m });
        var createdDto = await created.Content.ReadFromJsonAsync<GadgetDto>();

        var response = await host.Client.PutAsJsonAsync($"/gadgets/{createdDto!.Id}", new { price = 42m });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<GadgetDto>();
        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(createdDto.Id);
        dto.Price.ShouldBe(42m);
    }

    [Fact]
    public async Task PutGadgetPrice_WithUnknownId_Returns404()
    {
        var response = await host.Client.PutAsJsonAsync($"/gadgets/{Guid.NewGuid()}", new { price = 1m });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetGadgetById_AfterCreate_Returns200()
    {
        var created = await host.Client.PostAsJsonAsync("/gadgets", new { name = "lookup-me", price = 9m });
        var createdDto = await created.Content.ReadFromJsonAsync<GadgetDto>();

        var response = await host.Client.GetAsync($"/gadgets/{createdDto!.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<GadgetDto>();
        dto.ShouldNotBeNull();
        dto.Name.ShouldBe("lookup-me");
    }

    [Fact]
    public async Task GetGadgetById_WithUnknownId_Returns404()
    {
        var response = await host.Client.GetAsync($"/gadgets/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetGadgetList_AfterCreates_ReturnsPagedResponse()
    {
        await host.Client.PostAsJsonAsync("/gadgets", new { name = "list-a", price = 1m });
        await host.Client.PostAsJsonAsync("/gadgets", new { name = "list-b", price = 2m });

        // Filters to just the two rows this test created — the shared fixture DB accumulates rows across
        // every test in this class, so an unfiltered list can't assert an exact count deterministically.
        var response = await host.Client.GetAsync("/gadgets?filter=name:Contains:list-");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<GadgetDto>>();
        page.ShouldNotBeNull();
        page.PageSize.ShouldBeGreaterThan(0);
        page.TotalItemCount.ShouldBe(2);
        page.Items.ShouldContain(x => x.Name == "list-a" && x.Price == 1m);
        page.Items.ShouldContain(x => x.Name == "list-b" && x.Price == 2m);
    }

    [Fact]
    public async Task DeleteGadgetById_WithExistingId_RemovesRow()
    {
        var created = await host.Client.PostAsJsonAsync("/gadgets", new { name = "delete-me", price = 3m });
        var createdDto = await created.Content.ReadFromJsonAsync<GadgetDto>();

        var deleteResponse = await host.Client.DeleteAsync($"/gadgets/{createdDto!.Id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getResponse = await host.Client.GetAsync($"/gadgets/{createdDto.Id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
