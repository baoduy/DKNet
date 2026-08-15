using System.Collections.Concurrent;
using System.Reflection;
using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HookContext = EfCore.HookTests.Data.HookContext;

namespace EfCore.HookTests.Hooks;

/// <summary>
///     Verifies DKNET-HOOK-001: hooks must resolve scoped dependencies from the DbContext's own DI scope,
///     so request-scoped state (e.g. a per-request tenant/ownership provider) is visible to hooks.
/// </summary>
public class HookScopeResolutionTests : IAsyncLifetime
{
    #region Fields

    private SqliteConnection? _connection;
    private ServiceProvider _provider = null!;

    #endregion

    #region Methods

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _provider = new ServiceCollection()
            .AddLogging(builder => builder
                .SetMinimumLevel(LogLevel.Information)
                .AddProvider(new EnabledLoggingProvider()))
            .AddScoped<TestScopedMarker>()
            .AddDbContextWithHook<HookContext>(o =>
                o.UseSqlite(_connection).UseAutoConfigModel())
            .AddHook<HookContext, ScopedStateHook>()
            .BuildServiceProvider();

        // Ensure the schema exists outside any request scope.
        var db = _provider.GetRequiredService<HookContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_provider != null) await _provider.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Hook_WithScopedDependencySetInRequestScope_ObservesSameRequestScopedInstance()
    {
        // Arrange: request scope owns the marker AND the DbContext
        using var scope = _provider.CreateScope();
        var marker = scope.ServiceProvider.GetRequiredService<TestScopedMarker>();
        marker.RequestValue = "request-1";
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();

        ScopedStateHook.Reset();

        // Act
        await db.Set<CustomerProfile>().AddAsync(new CustomerProfile { Name = "Scoped A" });
        await db.SaveChangesAsync();

        // Assert: the hook observed the very same scoped instance, with its per-request value intact
        ScopedStateHook.ObservedValue.ShouldBe("request-1");
        ScopedStateHook.ObservedMarker.ShouldBeSameAs(marker);
    }

    [Fact]
    public async Task Hook_AcrossMultipleRequests_ObservesEachRequestsOwnScopedState()
    {
        // First request
        string? firstObserved;
        using (var scope = _provider.CreateScope())
        {
            var marker = scope.ServiceProvider.GetRequiredService<TestScopedMarker>();
            marker.RequestValue = "tenant-A";
            var db = scope.ServiceProvider.GetRequiredService<HookContext>();

            ScopedStateHook.Reset();
            await db.Set<CustomerProfile>().AddAsync(new CustomerProfile { Name = "Req A" });
            await db.SaveChangesAsync();

            ScopedStateHook.ObservedMarker.ShouldBeSameAs(marker);
            firstObserved = ScopedStateHook.ObservedValue;
        }

        // Second request must get a fresh scoped instance, not the first request's state
        using (var scope = _provider.CreateScope())
        {
            var marker = scope.ServiceProvider.GetRequiredService<TestScopedMarker>();
            marker.RequestValue.ShouldBeEmpty();
            marker.RequestValue = "tenant-B";
            var db = scope.ServiceProvider.GetRequiredService<HookContext>();

            ScopedStateHook.Reset();
            await db.Set<CustomerProfile>().AddAsync(new CustomerProfile { Name = "Req B" });
            await db.SaveChangesAsync();

            ScopedStateHook.ObservedMarker.ShouldBeSameAs(marker);
            ScopedStateHook.ObservedValue.ShouldBe("tenant-B");
        }

        firstObserved.ShouldBe("tenant-A");
    }

    [Fact]
    public async Task Hook_OnDbContextWithoutApplicationServiceProvider_ThrowsInvalidOperationException()
    {
        // A manually-constructed DbContext carries no application service provider, so hooks cannot
        // be resolved from its scope. This is a documented guard, not a silent failure path.
        var options = new DbContextOptionsBuilder<HookContext>()
            .UseSqlite("Data Source=no-app-provider.db")
            .UseAutoConfigModel()
            .AddInterceptors(new HookRunnerInterceptor(NullLogger<HookRunnerInterceptor>.Instance))
            .Options;

        await using var db = new HookContext(options);
        await db.Set<CustomerProfile>().AddAsync(new CustomerProfile { Name = "No provider" });

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        exception.Message.ShouldContain("has no application service provider");
    }

    [Fact]
    public void HookContext_ResolvedFromProvidedScopeProvider_UsesSameScopedInstances()
    {
        using var hooksProvider = new ServiceCollection()
            .AddScoped<TestScopedMarker>()
            .AddScoped<HookFactory>()
            .AddKeyedScoped<ScopedStateHook>(typeof(HookContext).FullName)
            .AddKeyedScoped<IHookBaseAsync>(typeof(HookContext).FullName,
                (p, k) => p.GetRequiredKeyedService<ScopedStateHook>(k))
            .BuildServiceProvider();

        using var scope = hooksProvider.CreateScope();
        var marker = scope.ServiceProvider.GetRequiredService<TestScopedMarker>();
        marker.RequestValue = "direct-scope";

        using var db = new HookContext(
            new DbContextOptionsBuilder<HookContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var hookContext = new DKNet.EfCore.Hooks.Internals.HookContext(scope.ServiceProvider, db);
        var hook = hookContext.BeforeSaveHooks.OfType<ScopedStateHook>().Single();

        hook.Marker.ShouldBeSameAs(marker);
        hookContext.Dispose();
    }

    [Fact]
    public async Task HookContext_DisposeAndDisposeAsync_DoNotThrow()
    {
        using var hooksProvider = new ServiceCollection()
            .AddScoped<TestScopedMarker>()
            .AddScoped<HookFactory>()
            .AddKeyedScoped<ScopedStateHook>(typeof(HookContext).FullName)
            .AddKeyedScoped<IHookBaseAsync>(typeof(HookContext).FullName,
                (p, k) => p.GetRequiredKeyedService<ScopedStateHook>(k))
            .BuildServiceProvider();

        using var scope = hooksProvider.CreateScope();
        using var db = new HookContext(
            new DbContextOptionsBuilder<HookContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var hookContext = new DKNet.EfCore.Hooks.Internals.HookContext(scope.ServiceProvider, db);
        hookContext.Dispose();
        hookContext.Dispose();

        var hookContext2 = new DKNet.EfCore.Hooks.Internals.HookContext(scope.ServiceProvider, db);
        await hookContext2.DisposeAsync();
        await hookContext2.DisposeAsync();
    }

    [Fact]
    public async Task Hook_OnFailedSave_RunsSaveChangesFailedCleanup()
    {
        // A save that fails mid-pipeline must still clean up the cached HookContext created by
        // SavingChangesAsync, without swallowing the database error.
        await using var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();

        using var failingProvider = new ServiceCollection()
            .AddLogging(builder => builder
                .SetMinimumLevel(LogLevel.Information)
                .AddProvider(new EnabledLoggingProvider()))
            .AddScoped<TestScopedMarker>()
            .AddDbContextWithHook<HookContext>(o =>
                o.UseSqlite(conn).UseAutoConfigModel())
            .AddHook<HookContext, ScopedStateHook>()
            .BuildServiceProvider();

        var bootstrap = failingProvider.GetRequiredService<HookContext>();
        await bootstrap.Database.EnsureCreatedAsync();

        // Auto-config names tables after the singular entity type.
        await bootstrap.Database.ExecuteSqlRawAsync("DROP TABLE CustomerProfile");

        var db = failingProvider.GetRequiredService<HookContext>();
        await db.Set<CustomerProfile>().AddAsync(new CustomerProfile { Name = "Will fail" });

        var exception = await Record.ExceptionAsync(() => db.SaveChangesAsync());
        var type = exception?.GetType().FullName ?? "no exception";
        exception.ShouldNotBeNull($"SaveChanges must surface the database error (type={type})");
        var chained = exception.Message + " | inner: " + (exception.InnerException?.Message ?? "");
        chained.Contains("no such table", StringComparison.Ordinal)
            .ShouldBeTrue($"expected table-missing error, got: {chained}");
    }

    [Fact]
    public void HookRunnerInterceptor_Dispose_DisposesCachedHookContexts()
    {
        // The synchronous Dispose path is only exercised when the interceptor dies while holding cached
        // HookContexts (normal saves empty the cache in SavedChangesAsync). Arm the cache with a real
        // HookContext and verify the interceptor still disposes it.
        var interceptor = new HookRunnerInterceptor(NullLogger<HookRunnerInterceptor>.Instance);

        using var hooksProvider = new ServiceCollection()
            .AddScoped<TestScopedMarker>()
            .AddScoped<HookFactory>()
            .AddKeyedScoped<ScopedStateHook>(typeof(HookContext).FullName)
            .AddKeyedScoped<IHookBaseAsync>(typeof(HookContext).FullName,
                (p, k) => p.GetRequiredKeyedService<ScopedStateHook>(k))
            .BuildServiceProvider();
        using var db = new HookContext(
            new DbContextOptionsBuilder<HookContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var cacheField = typeof(HookRunnerInterceptor).GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic);
        var cache = (ConcurrentDictionary<Guid, DKNet.EfCore.Hooks.Internals.HookContext>)cacheField!.GetValue(interceptor)!;
        cache[Guid.NewGuid()] = new DKNet.EfCore.Hooks.Internals.HookContext(hooksProvider, db);

        interceptor.Dispose();

        cache.ShouldBeEmpty();
    }

    #endregion

    #region Test helpers

    /// <summary>
    ///     A registered logger provider makes filtering effective; the provider itself is silent, so
    ///     this enables the information-level branches of <see cref="HookRunnerInterceptor" /> without
    ///     writing any log output during tests.
    /// </summary>
    private sealed class EnabledLoggingProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new SilentLogger();

        public void Dispose() { }

        private sealed class SilentLogger : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) { }
        }
    }

    #endregion
}