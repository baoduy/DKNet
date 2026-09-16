using DKNet.AspCore.Extensions.Endpoints;
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Specifications;
using DKNet.SlimBus.Extensions;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlimBus.Generators.Tests.Api.Crud;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace SlimBus.Generators.Tests.Api;

/// <summary>
///     A third real minimal-API host, separate from <see cref="GadgetTestHost" /> and
///     <see cref="GadgetAuthTestHost" />, mapping <c>MapGadgetCrud()</c> with the new by-name
///     <see cref="CrudMapOptions.Exclude(string[])" /> overload (DRK-1355 §5 scenarios 1, 2, 4). Kept out of
///     <see cref="GadgetTestHost" /> so that host's baseline groups (used by <c>GadgetCrudSliceTests</c>, an
///     `@existing` regression suite) never depend on the by-name overload, which the Acceptance-tests stage
///     leaves throwing <see cref="NotImplementedException" /> — exactly the isolation
///     <see cref="GadgetAuthTestHost" /> already established for <c>Configure</c> in DRK-1327.
/// </summary>
public sealed class GadgetExclusionTestHost : IAsyncLifetime, IDisposable
{
    private WebApplication? _app;
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    public void Dispose() => _connection?.Dispose();

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton<IMapper>(new Mapper(new TypeAdapterConfig()));
        builder.Services.AddDbContextWithHook<GadgetDbContext>((_, o) => o.UseSqlite(_connection));
        builder.Services.AddScoped<DbContext>(p => p.GetRequiredService<GadgetDbContext>());
        builder.Services.AddSpecRepo<GadgetDbContext>();

        builder.Services
            .AddSlimBusEfCoreInterceptor<GadgetDbContext>()
            .AddSlimMessageBus(mbb => mbb
                .AddJsonSerializer()
                .AddServicesFromAssembly(typeof(GadgetExclusionTestHost).Assembly)
                .AddChildBus(
                    "Memory",
                    mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(GadgetExclusionTestHost).Assembly)));

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GadgetDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        // Scenario 1: excluding the update member "Rename" by name keeps "UpdatePrice"'s {id} route published,
        // and leaves every untouched kind (GetById, GetList, Create, Delete, Action) published too (scenario 5).
        app.MapGroup("/gadgets-no-rename").MapGadgetCrud(o => o.Exclude("Rename"));

        // Scenario 4: excluding the update member "UpdatePrice" by name keeps "Rename"'s {id}/rename route
        // published at its own fixed address — excluding a sibling never moves it (R4).
        app.MapGroup("/gadgets-no-update-price").MapGadgetCrud(o => o.Exclude("UpdatePrice"));

        // Scenario 2: excluding the action member "Discontinue" by name keeps "Approve"'s {id}/approve route
        // published.
        app.MapGroup("/gadgets-no-discontinue").MapGadgetCrud(o => o.Exclude("Discontinue"));

        await app.StartAsync();
        _app = app;
        Client = app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        // Client stays null when InitializeAsync throws before reaching app.StartAsync() — e.g. today, while
        // CrudMapOptions.Exclude(string[]) is a NotImplementedException stub (DRK-1355 Acceptance-tests stage).
        Client?.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        if (_connection is not null) await _connection.DisposeAsync();
    }
}
