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
///     Dedicated <c>MapGetList</c> host for the default (unconfigured) page-size ceiling, seeded well past the
///     new 1000-row default so a request for the full ceiling actually proves 1000 rows are served, rather than
///     echoing back however many rows <see cref="PagingTestHost" />'s smaller seed happens to hold.
/// </summary>
public sealed class PagingCeilingTestHost : IAsyncLifetime
{
    #region Fields

    /// <summary>Seeded widget rows — &gt; 1000 so the default ceiling clamp is provable, not just echoed back.</summary>
    public const int SeededWidgetCount = 1100;

    private WebApplication? _app;

    #endregion

    #region Properties

    public HttpClient Client { get; private set; } = null!;

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

            for (var i = 1; i <= SeededWidgetCount; i++) db.Widgets.Add(new WidgetEntity(Guid.NewGuid(), $"widget-{i}"));

            await db.SaveChangesAsync();
        }

        app.MapGroup("/p").MapGetList<WidgetEntity, WidgetModel>("/widgets");
        app.MapOpenApi();

        await app.StartAsync();
        _app = app;
        Client = app.GetTestClient();
    }

    #endregion
}
