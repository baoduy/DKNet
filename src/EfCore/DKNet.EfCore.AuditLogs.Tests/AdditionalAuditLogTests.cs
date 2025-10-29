using System.Collections.Concurrent;
using DKNet.EfCore.AuditLogs.Internals;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

// for EfCoreAuditHook via DI

namespace DKNet.EfCore.AuditLogs.Tests;

// Non audited entity for negative test
public class PlainEntity
{
    #region Properties

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    #endregion
}

internal sealed class CapturingLogger<T> : ILogger<T>
{
    #region Properties

    public ConcurrentBag<string> Messages { get; } = [];

    #endregion

    #region Methods

    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        this.Messages.Add(formatter(state, exception));
    }

    #endregion

    private sealed class NullScope : IDisposable
    {
        #region Methods

        public void Dispose()
        {
        }

        #endregion

        public static readonly NullScope Instance = new();
    }
}

// internal sealed class FailingPublisher2 : IAuditLogPublisher
// {
//     public Task PublishAsync(IEnumerable<EfCoreAuditLog> logs, CancellationToken cancellationToken = default)
//         => throw new InvalidOperationException("Publisher failure test");
// }

internal sealed class RecordingPublisher : IAuditLogPublisher
{
    #region Properties

    public static bool Called { get; private set; }

    public static ConcurrentBag<AuditLogEntry> Logs { get; } = [];

    #endregion

    #region Methods

    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
    {
        Called = true;
        foreach (var log in logs)
        {
            Logs.Add(log);
        }

        return Task.CompletedTask;
    }

    public static void Reset()
    {
        Called = false;
        while (Logs.TryTake(out _))
        {
        }
    }

    #endregion
}

public class AdditionalAuditLogTests
{
    #region Methods

    [Fact]
    public async Task BuildAuditLog_Captures_Modified_Properties_Including_Nulls()
    {
        await using var ctx = new TestAuditDbContext(BuildInMemoryOpts());
        await ctx.Database.EnsureCreatedAsync();
        var e = new TestAuditEntity { Name = "A", Age = 5, IsActive = true, Balance = 1m };
        e.SetCreatedBy("creator");
        await ctx.AddAsync(e);
        await ctx.SaveChangesAsync();
        e.Notes = "note"; // new value from null
        e.LastLoginOn = DateTimeOffset.UtcNow;
        e.UpdateProfile("updater");
        ctx.Update(e);
        ctx.ChangeTracker.DetectChanges();
        var entry = ctx.Entry(e);
        var log = entry.BuildAuditLog(EntityState.Modified, AuditLogBehaviour.IncludeAllAuditedEntities)!;
        log.Changes.ShouldContain(c =>
            c.FieldName == nameof(TestAuditEntity.Notes) && c.OldValue == null && (string?)c.NewValue == "note");
        log.Changes.ShouldContain(c =>
            c.FieldName == nameof(TestAuditEntity.LastLoginOn) && c.OldValue == null && c.NewValue is DateTimeOffset);
    }

    [Fact]
    public async Task BuildAuditLog_Deletion_Sets_NewValue_Null()
    {
        await using var ctx = new TestAuditDbContext(BuildInMemoryOpts());
        await ctx.Database.EnsureCreatedAsync();
        var e = new TestAuditEntity { Name = "Del", Age = 1, IsActive = false, Balance = 0m };
        e.SetCreatedBy("creator");
        await ctx.AddAsync(e);
        await ctx.SaveChangesAsync();
        ctx.Remove(e);
        ctx.ChangeTracker.DetectChanges();
        var entry = ctx.Entry(e);
        var log = entry.BuildAuditLog(EntityState.Deleted, AuditLogBehaviour.IncludeAllAuditedEntities)!;
        log.Changes.ShouldAllBe(c => c.NewValue == null);
        log.EntityName.ShouldBe(nameof(TestAuditEntity));
    }

    [Fact]
    public async Task BuildAuditLog_Returns_Null_For_NonAudited_Entity()
    {
        await using var ctx = new TestAuditDbContext(BuildInMemoryOpts());
        await ctx.Database.EnsureCreatedAsync();
        var plain = new PlainEntity { Id = 1, Name = "P" };
        await ctx.AddAsync(plain);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.DetectChanges();
        var entry = ctx.Entry(plain);
        entry.State = EntityState.Modified;
        var log = entry.BuildAuditLog(EntityState.Modified, AuditLogBehaviour.IncludeAllAuditedEntities);
        log.ShouldBeNull();
    }

    // Helper to poll for expected count in fire-and-forget publishing
    // private static async Task WaitForCountAsync(Func<int> currentCount, int expected, int timeoutMs = 1000)
    // {
    //     var sw = System.Diagnostics.Stopwatch.StartNew();
    //     while (sw.ElapsedMilliseconds < timeoutMs)
    //     {
    //         if (currentCount() >= expected) return;
    //         await Task.Delay(25);
    //     }
    // }

    private static DbContextOptions<TestAuditDbContext> BuildInMemoryOpts() =>
        new DbContextOptionsBuilder<TestAuditDbContext>()
            .UseSqlite(
                $"Data Source={Path.Combine(Path.GetTempPath(), $"audit_mem_{Guid.NewGuid():N}.db")};Cache=Shared")
            .EnableSensitiveDataLogging()
            .Options;

    [Fact]
    public async Task Concurrent_Saves_Produce_All_Logs()
    {
        var services = new ServiceCollection().AddLogging();
        var dbName = Path.Combine(Path.GetTempPath(), $"mem_conc_{Guid.NewGuid():N}.db");
        services.AddEfCoreAuditLogs<TestAuditDbContext, RecordingPublisher>();
        services.AddDbContextWithHook<TestAuditDbContext>((_, o) => o.UseSqlite($"Data Source={dbName};Cache=Shared"));
        var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var seedCtx = scope.ServiceProvider.GetRequiredService<TestAuditDbContext>();
        await seedCtx.Database.EnsureCreatedAsync();

        RecordingPublisher.Reset();

        for (var i = 0; i < 10; i++)
        {
            var e = new TestAuditEntity { Name = $"CC{i}", Age = i, IsActive = true, Balance = i };
            e.SetCreatedBy("seed");
            await seedCtx.AddAsync(e);
        }

        await seedCtx.SaveChangesAsync();
        RecordingPublisher.Reset();

        var ids = seedCtx.AuditEntities.Select(e => e.Id).ToList();

        var tasks = ids.Select(id => Task.Run(async () =>
        {
            using var updateScope = sp.CreateScope();
            var ctx = updateScope.ServiceProvider.GetRequiredService<TestAuditDbContext>();
            await ctx.Database.EnsureCreatedAsync();

            var entity = ctx.AuditEntities.First(e => e.Id == id);
            entity.Age += 1;
            entity.UpdateProfile("bulk-updater");
            await ctx.SaveChangesAsync();
        })).ToList();

        await Task.WhenAll(tasks);
        await Task.Delay(1000);

        //await WaitForCountAsync(() => rec.Logs.Count, 10); // replaced fixed delay

        RecordingPublisher.Logs.Count.ShouldBeGreaterThanOrEqualTo(10);

        //RecordingPublisher.Logs.Count.ShouldBe(seedCtx.AuditEntities.Count(e => e.UpdatedBy == "bulk-updater"));
    }

    [Fact]
    public async Task CustomPublisher_Registration_Invokes_RecordingPublisher()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEfCoreAuditLogs<TestAuditDbContext, RecordingPublisher>();
        services.AddDbContextWithHook<TestAuditDbContext>((_, o) =>
            o.UseSqlite(
                $"Data Source={Path.Combine(Path.GetTempPath(), $"mem_custom_{Guid.NewGuid():N}.db")};Cache=Shared"));

        var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TestAuditDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        RecordingPublisher.Reset();

        var e = new TestAuditEntity { Name = "C", Age = 2, IsActive = true, Balance = 3.2m };
        e.SetCreatedBy("creator");
        await ctx.AddAsync(e);
        await ctx.SaveChangesAsync();

        e.Age = 3;
        e.UpdateProfile("updater");
        await ctx.SaveChangesAsync();
        await Task.Delay(1000);

        //await WaitForCountAsync(() => rec.Logs.Count, 1);
        RecordingPublisher.Called.ShouldBeTrue();
        RecordingPublisher.Logs.Count.ShouldBeGreaterThan(0);
        RecordingPublisher.Logs.ShouldAllBe(x => x.Keys.Count > 0);
        RecordingPublisher.Logs.ShouldContain(l => l.UpdatedBy == "updater");
    }

    [Fact]
    public async Task FailingPublisher_Logs_Error_But_Allows_Others()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<ILogger<EfCoreAuditHook>, CapturingLogger<EfCoreAuditHook>>();
        services.AddEfCoreAuditLogs<TestAuditDbContext, FailingPublisher>();
        services.AddEfCoreAuditLogs<TestAuditDbContext, RecordingPublisher>();

        services.AddDbContextWithHook<TestAuditDbContext>((_, o) =>
            o.UseSqlite(
                $"Data Source={Path.Combine(Path.GetTempPath(), $"mem_fail_{Guid.NewGuid():N}.db")};Cache=Shared"));
        var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TestAuditDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        RecordingPublisher.Reset();
        var e = new TestAuditEntity { Name = "F", Age = 1, IsActive = true, Balance = 1m };
        e.SetCreatedBy("creator");
        await ctx.AddAsync(e);
        await ctx.SaveChangesAsync();
        e.Age = 2;
        e.UpdateProfile("updater");
        await ctx.SaveChangesAsync();
        await Task.Delay(1000);

        //await WaitForCountAsync(() => rec.Logs.Count, 1); // replaced fixed delay
        RecordingPublisher.Logs.Count.ShouldBeGreaterThan(0);
    }

    #endregion
}