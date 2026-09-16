using System.Security.Claims;
using System.Text.Encodings.Web;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Specifications;
using DKNet.SlimBus.Extensions;
using FluentValidation;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
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

    public DbSet<Widget> Widgets => Set<Widget>();
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

    /// <summary>Exposes the host's root <see cref="IServiceProvider" /> — used by DRK-1326 tests to read
    /// <see cref="GadgetSaveAttemptRecorder" /> after a request completes.</summary>
    public IServiceProvider Services => _app!.Services;

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
        builder.Services.AddSingleton<GadgetSaveAttemptRecorder>();
        builder.Services.AddDbContextWithHook<GadgetDbContext>((_, o) => o.UseSqlite(_connection));
        builder.Services.AddHook<GadgetDbContext, GadgetDeleteRecordingHook>();
        builder.Services.AddScoped<DbContext>(p => p.GetRequiredService<GadgetDbContext>());
        builder.Services.AddSpecRepo<GadgetDbContext>();

        // DRK-1326 §5 scenario 2/4/7 fixture: only the groups below that call
        // AddFluentValidationAutoValidation() ever consult this.
        builder.Services.AddValidatorsFromAssemblyContaining<DeleteGadgetRequestValidator>();

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

        // DRK-1326: the generated delete route now binds its own request type (DeleteGadgetRequest) rather
        // than the plain 2-arg MapDeleteById, so a group with no validation filter registered is unaffected
        // (R1) while a guarded group below can attach a rule to it.
        app.MapGroup("/gadgets-request").MapGadgetCrud();

        var guardedGadgets = app.MapGroup("/gadgets-guarded");
        guardedGadgets.MapGadgetCrud();
        guardedGadgets.AddFluentValidationAutoValidation();

        app.MapGroup("/widgets").MapWidgetCrud();

        var guardedWidgets = app.MapGroup("/widgets-guarded");
        guardedWidgets.MapWidgetCrud();
        guardedWidgets.AddFluentValidationAutoValidation();

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

/// <summary>
///     Authenticates every request as whatever <see cref="ScopesHeader" />/<see cref="UserHeader" /> say, so
///     one running host can play Dana/Mei/Ravi (DRK-1327 §5 scenarios 1-3) by varying request headers rather
///     than reconfiguring the host. Mirrors the scheme-stub pattern in
///     <c>AspCore.Extensions.Tests/Fixtures/EndpointConfigSupport.cs</c>'s <c>TestAuthHandler</c>, adapted so
///     the scopes vary per request instead of per DI registration.
/// </summary>
public sealed class ScopedTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestScoped";

    /// <summary>Comma-separated scope claim values for this request; absent or empty means no scopes (Dana).</summary>
    public const string ScopesHeader = "X-Test-Scopes";

    /// <summary>The authenticated identity's name for this request; defaults to "anonymous-scoped-user".</summary>
    public const string UserHeader = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var user = Request.Headers.TryGetValue(UserHeader, out var u) ? u.ToString() : "anonymous-scoped-user";
        var scopes = Request.Headers.TryGetValue(ScopesHeader, out var s)
            ? s.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

        var claims = new List<Claim> { new(ClaimTypes.Name, user) };
        claims.AddRange(scopes.Select(scope => new Claim("scope", scope)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

/// <summary>
///     A second real minimal-API host, separate from <see cref="GadgetTestHost" />, wiring the test
///     authentication scheme plus <c>product.write</c>/<c>product.price</c> authorization policies and mapping
///     <c>MapGadgetCrud()</c> with per-route-configuration <c>CrudMapOptions.Configure</c> calls (DRK-1327 §5
///     scenarios 1, 2, 3, 6, 7). Kept out of <see cref="GadgetTestHost" /> so that host's baseline groups
///     (used by <c>GadgetCrudSliceTests</c>) never depend on <c>Configure</c>, which the Acceptance-tests stage
///     leaves throwing <see cref="NotImplementedException" />.
/// </summary>
public sealed class GadgetAuthTestHost : IAsyncLifetime, IDisposable
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
        builder.Services.AddDbContext<GadgetDbContext>(o => o.UseSqlite(_connection));
        builder.Services.AddScoped<DbContext>(p => p.GetRequiredService<GadgetDbContext>());
        builder.Services.AddSpecRepo<GadgetDbContext>();

        builder.Services
            .AddSlimBusEfCoreInterceptor<GadgetDbContext>()
            .AddSlimMessageBus(mbb => mbb
                .AddJsonSerializer()
                .AddServicesFromAssembly(typeof(GadgetAuthTestHost).Assembly)
                .AddChildBus(
                    "Memory",
                    mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(GadgetAuthTestHost).Assembly)));

        builder.Services
            .AddAuthentication(ScopedTestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopedTestAuthHandler>(ScopedTestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("product.write", p => p.RequireClaim("scope", "product.write"))
            .AddPolicy("product.price", p => p.RequireClaim("scope", "product.price"));

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GadgetDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        // Scenario 1: a route setting (by name) carries its own scope; the other update route is unaffected.
        app.MapGroup("/gadgets-route-scope")
            .MapGadgetCrud(o => o.Configure("UpdatePrice", b => b.RequireAuthorization("product.price")));

        // Scenario 2: an operation-kind setting applies the same scope to every route of that kind.
        app.MapGroup("/gadgets-op-scope")
            .MapGadgetCrud(o => o.Configure(CrudOp.Update, b => b.RequireAuthorization("product.write")));

        // Scenario 3: an operation-kind setting and a route setting both apply to the same route.
        app.MapGroup("/gadgets-both-scopes")
            .MapGadgetCrud(o => o
                .Configure(CrudOp.Update, b => b.RequireAuthorization("product.write"))
                .Configure("UpdatePrice", b => b.RequireAuthorization("product.price")));

        // Scenario 6: excluding a route also drops the settings that name it — no error, route just absent.
        // Uses the route-NAME form (R1: "Delete" is the Delete op's own route name), not the op-kind form:
        // only the name form reaches ValidateRouteNames, which is what R4 says still validates (then never
        // applies) a setting naming an excluded route.
        app.MapGroup("/gadgets-excluded-delete-scope")
            .MapGadgetCrud(o => o
                .Configure("Delete", b => b.RequireAuthorization("product.write"))
                .Exclude(CrudOp.Delete));

        // Scenario 7: a service that configures nothing is unchanged — every route stays open, even with the
        // authentication/authorization middleware above wired into the same host.
        app.MapGroup("/gadgets-unconfigured").MapGadgetCrud();

        await app.StartAsync();
        _app = app;
        Client = app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        // Client stays null when InitializeAsync throws before reaching app.StartAsync() — e.g. today, while
        // CrudMapOptions.Configure is a NotImplementedException stub (DRK-1327 Acceptance-tests stage).
        Client?.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        if (_connection is not null) await _connection.DisposeAsync();
    }
}
