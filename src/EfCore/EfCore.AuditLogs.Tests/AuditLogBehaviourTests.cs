// New tests to cover AuditLogBehaviour enum paths.

using System.Collections.Concurrent;
using System.Diagnostics;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace EfCore.AuditLogs.Tests;

[AuditLog]
internal sealed class AttributedAuditEntity : AuditedEntity<Guid>
{
    #region Properties

    public string Name { get; set; } = string.Empty;

    public int Value { get; set; }

    #endregion
}

// Intentionally NOT decorated with [AuditLog]
internal sealed class UnattributedAuditEntity : AuditedEntity<Guid>
{
    #region Properties

    public string Name { get; set; } = string.Empty;

    public int Value { get; set; }

    #endregion
}

internal sealed class BehaviourDbContext(DbContextOptions<BehaviourDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<AttributedAuditEntity> Attributed => Set<AttributedAuditEntity>();

    public DbSet<UnattributedAuditEntity> Unattributed => Set<UnattributedAuditEntity>();

    #endregion
}

internal sealed class BehaviourCapturingPublisher : IAuditLogPublisher
{
    #region Fields

    private static readonly ConcurrentBag<AuditLogEntry> _logs = [];

    #endregion

    #region Properties

    public static IReadOnlyCollection<AuditLogEntry> Logs => _logs;

    #endregion

    #region Methods

    public static void Clear()
    {
        while (_logs.TryTake(out _))
        {
        }
    }

    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
    {
        foreach (var l in logs) _logs.Add(l);

        return Task.CompletedTask;
    }

    #endregion
}

public class AuditLogBehaviourTests
{
    #region Methods

    private static ServiceProvider BuildProvider(AuditLogBehaviour behaviour, string dbPath)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEfCoreAuditLogs<BehaviourDbContext, BehaviourCapturingPublisher>(behaviour);
        services.AddDbContextWithHook<BehaviourDbContext>((_, o) =>
        {
            o.UseSqlite($"Data Source={dbPath}");
            o.EnableSensitiveDataLogging();
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task IncludeAllAuditedEntities_Includes_Both_Entities()
    {
        var db = NewDbPath();
        await using var provider = BuildProvider(AuditLogBehaviour.IncludeAllAuditedEntities, db);
        await using (var scope = provider.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<BehaviourDbContext>();
            await ctx.Database.EnsureCreatedAsync();
        }

        BehaviourCapturingPublisher.Clear();
        Guid attributedId, unattributedId;

        // Create (no logs expected on Added)
        await using (var scope = provider.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<BehaviourDbContext>();
            var a = new AttributedAuditEntity { Name = "A2", Value = 2 };
            a.SetCreatedBy("creator");
            var u = new UnattributedAuditEntity { Name = "U2", Value = 20 };
            u.SetCreatedBy("creator");
            ctx.AddRange(a, u);
            await ctx.SaveChangesAsync();
            attributedId = a.Id;
            unattributedId = u.Id;
        }

        BehaviourCapturingPublisher.Logs.Count.ShouldBe(2);
        BehaviourCapturingPublisher.Clear();

        // Update both
        await using (var scope = provider.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<BehaviourDbContext>();
            var a = await ctx.Attributed.FirstAsync(x => x.Id == attributedId);
            var u = await ctx.Unattributed.FirstAsync(x => x.Id == unattributedId);
            a.Value = 22;
            a.SetUpdatedBy("upd");
            u.Value = 220;
            u.SetUpdatedBy("upd");
            await ctx.SaveChangesAsync();
        }

        await WaitForCountAsync(() => BehaviourCapturingPublisher.Logs.Count, 2);

        BehaviourCapturingPublisher.Logs.Count.ShouldBe(2);
        BehaviourCapturingPublisher.Logs.ShouldContain(l => l.EntityName == nameof(AttributedAuditEntity));
        BehaviourCapturingPublisher.Logs.ShouldContain(l => l.EntityName == nameof(UnattributedAuditEntity));
        BehaviourCapturingPublisher.Logs.ShouldAllBe(l => l.Action == AuditLogAction.Updated);
    }

    private static string NewDbPath() => Path.Combine(Path.GetTempPath(), $"behaviour_{Guid.NewGuid():N}.db");

    [Fact]
    public async Task OnlyAttributedAuditedEntities_Ignores_Unattributed()
    {
        var db = NewDbPath();
        await using var provider = BuildProvider(AuditLogBehaviour.OnlyAttributedAuditedEntities, db);
        await using (var scope = provider.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<BehaviourDbContext>();
            await ctx.Database.EnsureCreatedAsync();
        }

        BehaviourCapturingPublisher.Clear();
        Guid attributedId, unattributedId;

        // Create (no logs expected on Added)
        await using (var scope = provider.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<BehaviourDbContext>();
            var a = new AttributedAuditEntity { Name = "A1", Value = 1 };
            a.SetCreatedBy("creator");
            var u = new UnattributedAuditEntity { Name = "U1", Value = 10 };
            u.SetCreatedBy("creator");
            ctx.AddRange(a, u);
            await ctx.SaveChangesAsync();
            attributedId = a.Id;
            unattributedId = u.Id;
        }

        // Ensure still no logs
        BehaviourCapturingPublisher.Logs.Count.ShouldBe(1);
        BehaviourCapturingPublisher.Clear();

        // Update both
        await using (var scope = provider.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<BehaviourDbContext>();
            var a = await ctx.Attributed.FirstAsync(x => x.Id == attributedId);
            var u = await ctx.Unattributed.FirstAsync(x => x.Id == unattributedId);
            a.Value = 2;
            a.SetUpdatedBy("upd");
            u.Value = 11;
            u.SetUpdatedBy("upd");
            await ctx.SaveChangesAsync();
        }

        await WaitForCountAsync(() => BehaviourCapturingPublisher.Logs.Count, 1);

        BehaviourCapturingPublisher.Logs.Count.ShouldBe(1);
        BehaviourCapturingPublisher.Logs.ShouldAllBe(l => l.EntityName == nameof(AttributedAuditEntity));
        BehaviourCapturingPublisher.Logs.ShouldAllBe(l => l.Action == AuditLogAction.Updated);
    }

    [Fact]
    public async Task OnlyAttributedAuditedEntities_Updates_Still_Filtered()
    {
        var db = NewDbPath();
        await using var provider = BuildProvider(AuditLogBehaviour.OnlyAttributedAuditedEntities, db);
        await using (var scope = provider.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<BehaviourDbContext>();
            await ctx.Database.EnsureCreatedAsync();
        }

        BehaviourCapturingPublisher.Clear();
        Guid unattributedId;
        Guid attributedId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<BehaviourDbContext>();
            var a = new AttributedAuditEntity { Name = "A3", Value = 3 };
            a.SetCreatedBy("creator");
            var u = new UnattributedAuditEntity { Name = "U3", Value = 30 };
            u.SetCreatedBy("creator");
            ctx.AddRange(a, u);
            await ctx.SaveChangesAsync();
            unattributedId = u.Id;
            attributedId = a.Id;
        }

        BehaviourCapturingPublisher.Logs.Count.ShouldBe(1);
        BehaviourCapturingPublisher.Clear();

        await using (var scope = provider.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<BehaviourDbContext>();
            var a = await ctx.Attributed.FirstAsync(e => e.Id == attributedId);
            var u = await ctx.Unattributed.FirstAsync(e => e.Id == unattributedId);
            a.Value = 300;
            a.SetUpdatedBy("upd");
            u.Value = 400;
            u.SetUpdatedBy("upd");
            await ctx.SaveChangesAsync();
        }

        await WaitForCountAsync(() => BehaviourCapturingPublisher.Logs.Count, 1);

        BehaviourCapturingPublisher.Logs.Count.ShouldBe(1);
        BehaviourCapturingPublisher.Logs.ShouldAllBe(l => l.EntityName == nameof(AttributedAuditEntity));
        BehaviourCapturingPublisher.Logs.ShouldAllBe(l => l.Action == AuditLogAction.Updated);
    }

    private static async Task WaitForCountAsync(Func<int> current, int expected, int timeoutMs = 2000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (current() >= expected) return;

            await Task.Delay(50);
        }
    }

    #endregion
}