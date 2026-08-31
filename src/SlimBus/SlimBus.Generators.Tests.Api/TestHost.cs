using DKNet.AspCore.Extensions.Endpoints;
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
using SlimBus.Generators.Tests.Domain.Catalog;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace SlimBus.Generators.Tests.Api;

/// <summary>Minimal <see cref="DbContext" /> fixture for the generated Gadget CRUD slice.</summary>
public sealed class GadgetDbContext(DbContextOptions<GadgetDbContext> options) : DbContext(options)
{
    public DbSet<Gadget> Gadgets => Set<Gadget>();
}

/// <summary>
///     Real minimal-API host proving Task 9's end-to-end generated CRUD slice: a real in-memory
///     <see cref="SlimMessageBus.IMessageBus" /> dispatching to the generated handlers, a real
///     <c>IRepositorySpec</c> backed by SQLite, and <c>/gadgets/...</c> mapped entirely by the generated
///     <c>MapGadgetCrud()</c> extension — zero hand-written request/handler/endpoint code.
/// </summary>
public sealed class GadgetTestHost : IAsyncLifetime, IDisposable
{
    private WebApplication? _app;
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    public void Dispose() => _connection?.Dispose();

    public async Task InitializeAsync()
    {
        // Kept open for the fixture's lifetime: SQLite's ":memory:" database is dropped once its one
        // connection closes, so this connection (not EF's per-scope ones) owns the database's lifetime.
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton<IMapper>(new Mapper(new TypeAdapterConfig()));
        builder.Services.AddDbContext<GadgetDbContext>(o => o.UseSqlite(_connection));
        builder.Services.AddScoped<DbContext>(p => p.GetRequiredService<GadgetDbContext>());
        builder.Services.AddSpecRepo<GadgetDbContext>();

        builder.Services
            .AddSlimBusEfCoreInterceptor<GadgetDbContext>()
            .AddSlimMessageBus(mbb => mbb
                .AddJsonSerializer()
                .AddServicesFromAssembly(typeof(GadgetTestHost).Assembly)
                .AddChildBus(
                    "Memory",
                    mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(GadgetTestHost).Assembly)));

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GadgetDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        app.MapGroup("/gadgets").MapGadgetCrud();
        // Same generated MapGadgetCrud(), same underlying data — only the registration options differ, proving
        // an actions-excluded group still serves updates while dropping the action route(s) (spec §3.7).
        app.MapGroup("/gadgets-no-actions").MapGadgetCrud(o => o.Exclude(CrudOp.Action));

        await app.StartAsync();
        _app = app;
        Client = app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        if (_connection is not null) await _connection.DisposeAsync();
    }
}
