using System.Collections.Concurrent;
using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Specifications;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Extensions.Tests.Fixtures;

/// <summary>Captures every <see cref="AuditLogEntry" /> published for <see cref="DeleteByIdDbContext" /> during a test run.</summary>
public sealed class DeleteByIdAuditPublisher : IAuditLogPublisher
{
    #region Fields

    private static readonly ConcurrentBag<AuditLogEntry> _received = [];

    #endregion

    #region Properties

    public static IReadOnlyCollection<AuditLogEntry> Received => _received;

    #endregion

    #region Methods

    public static void Clear()
    {
        while (_received.TryTake(out _))
        {
        }
    }

    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
    {
        foreach (var l in logs) _received.Add(l);
        return Task.CompletedTask;
    }

    #endregion
}

/// <summary>Captures every domain event published for <see cref="DeleteByIdDbContext" /> during a test run.</summary>
public sealed class DeleteByIdEventPublisher : DefaultEventPublisher
{
    #region Fields

    private static readonly ConcurrentBag<object> _events = [];

    #endregion

    #region Properties

    public static IReadOnlyCollection<object> Events => _events;

    #endregion

    #region Methods

    public static void Clear()
    {
        while (_events.TryTake(out _))
        {
        }
    }

    public override Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        _events.Add(eventObj);
        return Task.CompletedTask;
    }

    #endregion
}

/// <summary>
///     Real minimal-API host for the DRK-703 §5 <c>MapDeleteById</c> scenarios — a real, relational (SQLite)
///     <c>IRepositorySpec</c> with real <c>DKNet.EfCore.AuditLogs</c> / <c>DKNet.EfCore.Events</c> hooks
///     wired to the same <c>SaveChangesAsync</c> pipeline the handler goes through, plus a route group that
///     requires authorization, so the conflict/audit/event/authorization scenarios are proven against real
///     mechanisms rather than status-code assertions alone.
/// </summary>
public sealed class DeleteByIdTestHost : IAsyncLifetime
{
    #region Fields

    private WebApplication? _app;
    private SqliteConnection? _connection;

    #endregion

    #region Properties

    public HttpClient Client { get; private set; } = null!;

    public IServiceProvider Services => _app!.Services;

    #endregion

    #region Methods

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

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        // EF Core's Sqlite provider only issues "PRAGMA foreign_keys=1" when IT opens the connection; since
        // this connection is opened here (kept alive for the ":memory:" DB's lifetime) and handed to EF
        // already-open, that pragma never runs — set it explicitly so the conflict scenario's FK actually
        // enforces, instead of silently succeeding like the InMemory provider would.
        await using (var pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            await pragma.ExecuteNonQueryAsync();
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton<IMapper>(new Mapper(new TypeAdapterConfig()));
        builder.Services.AddOpenApi();

        builder.Services.AddDbContextWithHook<DeleteByIdDbContext>(o => o.UseSqlite(_connection));
        builder.Services.AddScoped<DbContext>(p => p.GetRequiredService<DeleteByIdDbContext>());
        builder.Services.AddSpecRepo<DeleteByIdDbContext>();
        builder.Services.AddEfCoreAuditLogs<DeleteByIdDbContext, DeleteByIdAuditPublisher>();
        builder.Services.AddEventPublisher<DeleteByIdDbContext, DeleteByIdEventPublisher>();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<TestAuthSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, o => o.Authenticated = false);
        builder.Services.AddAuthorization();

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeleteByIdDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        var group = app.MapGroup("/d");
        group.MapGetById<WidgetEntity, WidgetModel>("/widgets/{id}");
        group.MapDeleteById<WidgetEntity>("/widgets/{id}");
        group.MapDeleteById<AuditedWidgetEntity>("/audited-widgets/{id}");

        app.MapGroup("/d-secure").RequireAuthorization().MapDeleteById<WidgetEntity>("/widgets/{id}");

        app.MapOpenApi();

        await app.StartAsync();
        _app = app;
        Client = app.GetTestClient();
    }

    #endregion
}
