using System.Net;
using System.Text.Json;
using AspCore.Extensions.Tests.Fixtures;
using AspCore.Extensions.Tests.TestEntities;
using DKNet.EfCore.AuditLogs;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     DRK-703 §5 acceptance suite for the generic <c>MapDeleteById&lt;TEntity&gt;</c> endpoint — every
///     <c>@integration</c> scenario against a real relational (SQLite) <c>IRepositorySpec</c> store, so
///     the conflict, audit-trail and domain-event scenarios exercise the actual mechanisms (foreign-key
///     enforcement, EF Core save-pipeline interceptors) rather than status codes alone. The minor-version
///     compatibility invariant carries no dedicated test here — DRK-703 §3 names the pre-existing suite passing
///     unchanged as its evidence.
/// </summary>
public class MapDeleteByIdTests(DeleteByIdTestHost host) : IClassFixture<DeleteByIdTestHost>
{
    #region Methods

    [Fact]
    public async Task DeleteExistingWidget_Returns204NoContent_AndSubsequentFetchReturns404()
    {
        var id = Guid.NewGuid();
        await SeedWidgetAsync(id, "Rotary Valve RV-200");

        var response = await host.Client.DeleteAsync($"/d/widgets/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();

        var getResponse = await host.Client.GetAsync($"/d/widgets/{id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUnknownWidget_Returns404_AndChangesNothing()
    {
        var id = Guid.NewGuid();

        var response = await host.Client.DeleteAsync($"/d/widgets/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DeleteByIdDbContext>();
        (await db.Widgets.AnyAsync(w => w.Id == id)).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteWidgetWithReferencingStockRecord_Returns409Conflict_AndWidgetSurvives()
    {
        var widgetId = Guid.NewGuid();
        await SeedWidgetAsync(widgetId, "Rotary Valve RV-200");
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeleteByIdDbContext>();
            db.StockRecords.Add(new StockRecordEntity(Guid.NewGuid(), widgetId, "Jurong East"));
            await db.SaveChangesAsync();
        }

        var response = await host.Client.DeleteAsync($"/d/widgets/{widgetId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var getResponse = await host.Client.GetAsync($"/d/widgets/{widgetId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteAuditedWidget_RecordsTheRemovalInTheAuditTrail()
    {
        DeleteByIdAuditPublisher.Clear();
        var id = Guid.NewGuid();
        await SeedAuditedWidgetAsync(id, "Rotary Valve RV-200");

        var response = await host.Client.DeleteAsync($"/d/audited-widgets/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        DeleteByIdAuditPublisher.Received.ShouldContain(l =>
            l.EntityName == nameof(AuditedWidgetEntity) && l.Action == AuditLogAction.Deleted);
    }

    [Fact]
    public async Task DeleteAuditedWidget_SubscriberRecordsTheRemoval_AndItsDomainEventIsDispatched()
    {
        DeleteByIdAuditPublisher.Clear();
        DeleteByIdEventPublisher.Clear();
        var id = Guid.NewGuid();
        await SeedAuditedWidgetAsync(id, "Rotary Valve RV-200");

        var response = await host.Client.DeleteAsync($"/d/audited-widgets/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The save-time subscriber (the audit-log hook) recorded the removal ...
        DeleteByIdAuditPublisher.Received.ShouldContain(l =>
            l.EntityName == nameof(AuditedWidgetEntity) && l.Action == AuditLogAction.Deleted);

        // ... and the widget's declared [RaisesEvent] domain event was dispatched to its handler.
        DeleteByIdEventPublisher.Events.OfType<WidgetRemovedEvent>()
            .ShouldContain(e => e.Id == id && e.Name == "Rotary Valve RV-200");
    }

    [Fact]
    public async Task DeleteUnderAuthorizedRouteGroup_UnauthenticatedCaller_Returns401_AndWidgetSurvives()
    {
        var id = Guid.NewGuid();
        await SeedWidgetAsync(id, "Rotary Valve RV-200");

        var response = await host.Client.DeleteAsync($"/d-secure/widgets/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DeleteByIdDbContext>();
        (await db.Widgets.AnyAsync(w => w.Id == id)).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAndFetchById_DocumentTheSameStandardResponseOutcomes()
    {
        var response = await host.Client.GetAsync("/openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operations = document.RootElement.GetProperty("paths").GetProperty("/d/widgets/{id}");

        // Compare every outcome BUT each operation's own success shape (fetch's 200 body vs delete's 204
        // no-body) — DRK-703 §5 asks that the shared error/edge outcomes match, not the success code itself.
        var getOutcomes = ResponseCodesExcluding(operations.GetProperty("get"), "200");
        var deleteOutcomes = ResponseCodesExcluding(operations.GetProperty("delete"), "204");

        deleteOutcomes.ShouldBe(getOutcomes);
    }

    private static HashSet<string> ResponseCodesExcluding(JsonElement operation, string excluded) =>
        operation.GetProperty("responses").EnumerateObject()
            .Select(p => p.Name)
            .Where(code => code != excluded)
            .ToHashSet();

    private async Task SeedWidgetAsync(Guid id, string name)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DeleteByIdDbContext>();
        db.Widgets.Add(new WidgetEntity(id, name));
        await db.SaveChangesAsync();
    }

    private async Task SeedAuditedWidgetAsync(Guid id, string name)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DeleteByIdDbContext>();
        db.AuditedWidgets.Add(new AuditedWidgetEntity(id, name, "seed"));
        await db.SaveChangesAsync();
    }

    #endregion
}
