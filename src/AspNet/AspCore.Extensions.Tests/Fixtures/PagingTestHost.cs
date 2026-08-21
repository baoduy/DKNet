using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.EfCore.Specifications;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Extensions.Tests.Fixtures;

/// <summary>
///     Dedicated <c>MapGetList</c> paging host, seeded with enough rows to exercise real clamping, walking-pages,
///     and ordering behaviour (the 3-widget <see cref="EndpointTestHost" /> is too small to prove a 100-row
///     ceiling or a 20-row default actually clamp, rather than simply echoing a small total back).
/// </summary>
public sealed class PagingTestHost : IAsyncLifetime
{
    #region Fields

    /// <summary>Total non-audited <see cref="WidgetEntity" /> rows seeded — &gt; 100 so ceiling/default clamps are provable.</summary>
    public const int SeededWidgetCount = 205;

    private WebApplication? _app;

    #endregion

    #region Properties

    public HttpClient Client { get; private set; } = null!;

    /// <summary>Ids of every seeded widget, oldest-created first (index 0), for walking-pages/order assertions.</summary>
    public List<Guid> SeededWidgetIds { get; } = [];

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        Client.Dispose();
        if (_app is null) return;
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton<IMapper>(new Mapper(new TypeAdapterConfig()));
        builder.Services.AddOpenApi();
        var dbName = Guid.NewGuid().ToString("N");
        builder.Services.AddDbContext<WidgetDbContext>(o => o.UseInMemoryDatabase(dbName));
        builder.Services.AddScoped<DbContext>(p => p.GetRequiredService<WidgetDbContext>());
        builder.Services.AddSpecRepo<WidgetDbContext>();

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WidgetDbContext>();

            for (var i = 1; i <= SeededWidgetCount; i++)
            {
                var id = new Guid($"00000000-0000-0000-0000-{i:D12}");
                SeededWidgetIds.Add(id);
                db.Widgets.Add(new WidgetEntity(id, $"widget-{i}"));
            }

            // Two rows share a CreatedOn instant to prove Id is the tie-break, one is strictly newer.
            var tieInstant = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            db.Gadgets.AddRange(
                new GadgetEntity(
                    new Guid("00000000-0000-0000-0000-000000000101"), "gadget-tie-low", "seed", tieInstant),
                new GadgetEntity(
                    new Guid("00000000-0000-0000-0000-000000000102"), "gadget-tie-high", "seed", tieInstant),
                new GadgetEntity(
                    new Guid("00000000-0000-0000-0000-000000000001"), "gadget-newest", "seed",
                    tieInstant.AddDays(1)));

            await db.SaveChangesAsync();
        }

        var group = app.MapGroup("/p");
        group.MapGetList<WidgetEntity, WidgetModel>("/widgets");
        group.MapGetList<GadgetEntity, GadgetModel>("/gadgets");
        app.MapOpenApi();

        await app.StartAsync();
        _app = app;
        Client = app.GetTestClient();
    }

    #endregion
}
