using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace SlimBus.Generators.Tests.Api;

/// <summary>
///     DRK-1326 §5 acceptance tests ("give every generated delete route its own request object"). Each test
///     is named after its Gherkin scenario. <c>AccountGroup</c>/<c>Account</c> from the spec map onto this
///     project's <see cref="SlimBus.Generators.Tests.Domain.Catalog.Gadget" />/<see cref="SlimBus.Generators.Tests.Domain.Catalog.Widget" />
///     fixtures; "holds N accounts" maps onto a Gadget having N Widgets whose <c>GadgetId</c> points at it.
/// </summary>
public sealed class DeleteRouteRequestTypeTests(GadgetTestHost host) : IClassFixture<GadgetTestHost>
{
    private async Task<GadgetDto> CreateGadgetAsync(string name)
    {
        var response = await host.Client.PostAsJsonAsync("/gadgets", new { name, price = 1m });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GadgetDto>())!;
    }

    private async Task<WidgetDto> CreateWidgetAsync(string name, Guid gadgetId)
    {
        var response = await host.Client.PostAsJsonAsync("/widgets", new { name, gadgetId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WidgetDto>())!;
    }

    [Fact]
    public async Task AnEmptyAccountGroupIsDeleted()
    {
        var gadget = await CreateGadgetAsync("treasury-ops-1");

        var response = await host.Client.DeleteAsync($"/gadgets-guarded/{gadget.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.Client.GetAsync($"/gadgets/{gadget.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AGroupThatStillHoldsAccountsIsRefused()
    {
        var gadget = await CreateGadgetAsync("treasury-ops-2");
        await CreateWidgetAsync("acc-1", gadget.Id);
        await CreateWidgetAsync("acc-2", gadget.Id);
        await CreateWidgetAsync("acc-3", gadget.Id);

        var response = await host.Client.DeleteAsync($"/gadgets-guarded/{gadget.Id}");

        response.IsSuccessStatusCode.ShouldBeFalse();
        (await host.Client.GetAsync($"/gadgets/{gadget.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AServiceThatRegistersNoRuleIsUnaffected()
    {
        var gadget = await CreateGadgetAsync("treasury-ops-3");
        await CreateWidgetAsync("acc-4", gadget.Id);
        await CreateWidgetAsync("acc-5", gadget.Id);
        await CreateWidgetAsync("acc-6", gadget.Id);

        var response = await host.Client.DeleteAsync($"/gadgets-request/{gadget.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.Client.GetAsync($"/gadgets/{gadget.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ADeleteRuleStaysConfinedToItsOwnRecordType()
    {
        var gadget = await CreateGadgetAsync("treasury-ops-4");
        var widget = await CreateWidgetAsync("ACC-1001", gadget.Id);

        var response = await host.Client.DeleteAsync($"/widgets-guarded/{widget.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.Client.GetAsync($"/widgets/{widget.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletingNeedsNoRequestBody()
    {
        var gadget = await CreateGadgetAsync("treasury-ops-5");

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/gadgets-request/{gadget.Id}");
        // Deliberately no Content set — proves the route never requires a body.
        var response = await host.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.Client.GetAsync($"/gadgets/{gadget.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletingARecordThatIsNotThere()
    {
        var response = await host.Client.DeleteAsync($"/gadgets-request/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ARefusedDeleteLeavesNoTraceBehind()
    {
        var gadget = await CreateGadgetAsync("treasury-ops-6");
        await CreateWidgetAsync("acc-7", gadget.Id);
        await CreateWidgetAsync("acc-8", gadget.Id);
        await CreateWidgetAsync("acc-9", gadget.Id);

        var response = await host.Client.DeleteAsync($"/gadgets-guarded/{gadget.Id}");

        response.IsSuccessStatusCode.ShouldBeFalse();
        var recorder = host.Services.GetRequiredService<GadgetSaveAttemptRecorder>();
        recorder.DeletedGadgetIds.ShouldNotContain(gadget.Id);
        (await host.Client.GetAsync($"/gadgets/{gadget.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
