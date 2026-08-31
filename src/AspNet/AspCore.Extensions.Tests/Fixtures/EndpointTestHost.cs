using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.EfCore.Specifications;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace AspCore.Extensions.Tests.Fixtures;

/// <summary>
///     Builds a real minimal-API host — an in-memory <see cref="SlimMessageBus.IMessageBus" /> dispatching to real
///     handlers, and a real <see cref="IRepositorySpec" /> backed by EF Core's InMemory provider — with every
///     <c>FluentsEndpointMapperExtensions</c> mapper mapped once under <c>/t/...</c>, so tests exercise actual HTTP
///     dispatch (verb, route, status, response shape) rather than asserting on the returned builder's type.
/// </summary>
public sealed class EndpointTestHost : IAsyncLifetime
{
    #region Fields

    private WebApplication? _app;

    #endregion

    #region Properties

    public HttpClient Client { get; private set; } = null!;

    /// <summary>Id of the seeded "second" widget, positioned between two others for MapGetList ordering assertions.</summary>
    public Guid SeededWidgetId { get; } = new("00000000-0000-0000-0000-000000000002");

    /// <summary>Id of the int-keyed sprocket reserved for the delete scenario, so no other test depends on it.</summary>
    public int DeletableSprocketId { get; } = 99;

    /// <summary>The host's DI container — lets tests inspect endpoint metadata via <see cref="EndpointDataSource" />.</summary>
    public IServiceProvider Services => _app!.Services;

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
        // Capture the name once — AddDbContext's options delegate re-runs on every scope resolution, so a fresh
        // Guid generated inline here would hand each HTTP request its own empty in-memory database.
        var dbName = Guid.NewGuid().ToString("N");
        builder.Services.AddDbContext<WidgetDbContext>(o => o.UseInMemoryDatabase(dbName));
        builder.Services.AddScoped<DbContext>(p => p.GetRequiredService<WidgetDbContext>());
        builder.Services.AddSpecRepo<WidgetDbContext>();

        builder.Services.AddSlimMessageBus(mbb => mbb
            .AddJsonSerializer()
            .AddServicesFromAssembly(typeof(EndpointTestHost).Assembly)
            .AddChildBus(
                "Memory",
                mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(EndpointTestHost).Assembly)));

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WidgetDbContext>();
            db.Widgets.AddRange(
                new WidgetEntity(new Guid("00000000-0000-0000-0000-000000000001"), "first"),
                new WidgetEntity(SeededWidgetId, "second"),
                new WidgetEntity(new Guid("00000000-0000-0000-0000-000000000003"), "third"));

            // Id 99 exists only for the delete scenario, so removing it cannot disturb the ordering
            // and lookup assertions the other non-Guid-key tests make against ids 1-3.
            db.Sprockets.AddRange(
                new SprocketEntity(1, "sprocket-one"),
                new SprocketEntity(2, "sprocket-two"),
                new SprocketEntity(3, "sprocket-three"),
                new SprocketEntity(DeletableSprocketId, "sprocket-doomed"));

            db.Coupons.AddRange(
                new CouponEntity("SUMMER-2026", "Summer sale"),
                new CouponEntity("WINTER-2026", "Winter sale"));

            await db.SaveChangesAsync();
        }

        MapAllTestEndpoints(app);

        await app.StartAsync();
        _app = app;
        Client = app.GetTestClient();
    }

    private static void MapAllTestEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/t");

        group.MapDelete<EchoNoResponseCommand>("/delete-no-response");
        // MapDelete<TCommand, TResponse> ("with response") is NOT mapped here on purpose: it binds its command
        // parameter with no [FromBody]/[AsParameters], and ASP.NET Core's minimal-API inferred body binding does
        // not support DELETE — mapping it poisons this shared TestServer's endpoint-metadata build for every
        // OTHER route in this fixture (metadata inference runs once for the whole composite endpoint set on the
        // first request). See MapDeleteWithResponseTests for the isolated reproduction.

        group.MapGet<FindWidgetQuery, WidgetResult>("/get-query");
        group.MapGetPage<ListWidgetsPageQuery, WidgetResult>("/get-page");
        group.MapGetById<WidgetEntity, WidgetModel>("/widgets/{id}");
        group.MapGetList<WidgetEntity, WidgetModel>("/widgets");

        // Non-Guid keys: an int key (a struct key the framework does not special-case) and a string key
        // (which does not implement IParsable, so it proves minimal-API binding still resolves it).
        group.MapGetById<SprocketEntity, int, SprocketModel>("/sprockets/{id}");
        group.MapGetList<SprocketEntity, int, SprocketModel>("/sprockets");
        group.MapDeleteById<SprocketEntity, int>("/sprockets/{id}");
        group.MapGetById<CouponEntity, string, CouponModel>("/coupons/{id}");

        group.MapPatch<EchoNoResponseCommand>("/patch-no-response");
        group.MapPatch<RenameWidgetCommand, WidgetResult>("/patch-with-response");

        group.MapPost<CreateNoResponseCommand>("/post-no-response-create");
        group.MapPost<EchoNoResponseCommand>("/post-no-response-update");
        group.MapPost<CreateWidgetCommand, WidgetResult>("/post-with-response-create");
        group.MapPost<RenameWidgetCommand, WidgetResult>("/post-with-response-update");
        group.MapPost<FailingWidgetCommand, WidgetResult>("/post-fail-with-response");
        group.MapPost<FailingNoResponseCommand>("/post-fail-no-response");

        group.MapPut<EchoNoResponseCommand>("/put-no-response");
        group.MapPut<RenameWidgetCommand, WidgetResult>("/put-with-response");
        group.MapPutById<RenameThingRequest, Guid, string>("/things/{id}");
        group.MapActionById<ArchiveThingRequest, Guid, string>("/things/{id}/archive", "PATCH");
    }

    #endregion
}
