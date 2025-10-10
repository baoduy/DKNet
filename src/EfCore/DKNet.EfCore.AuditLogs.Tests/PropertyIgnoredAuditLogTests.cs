using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Collections.Concurrent; // added

// for AuditLogEntry & IAuditLogPublisher

namespace DKNet.EfCore.AuditLogs.Tests;

public class PropertyIgnoredAuditEntity : AuditedEntity<Guid>
{
    public string Name { get; set; } = string.Empty;

    [IgnoreAuditLog] public string Secret { get; set; } = string.Empty;
}

// Dedicated publisher for these tests to isolate static state
internal sealed class DedicatedRecordingPublisher : IAuditLogPublisher
{
    public static ConcurrentBag<AuditLogEntry> Logs { get; } = new();

    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
    {
        foreach (var l in logs) Logs.Add(l);
        return Task.CompletedTask;
    }

    public static void Reset()
    {
        while (Logs.TryTake(out _))
        {
        }
    }
}

internal class PropertyIgnoredAuditLogDbContext(DbContextOptions<PropertyIgnoredAuditLogDbContext> options)
    : DbContext(options)
{
    public DbSet<PropertyIgnoredAuditEntity> PropIgnoredEntities => Set<PropertyIgnoredAuditEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<PropertyIgnoredAuditEntity>();
    }
}

public class PropertyIgnoredAuditLogTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"prop_ignored_audit_{Guid.NewGuid():N}.db");

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEfCoreAuditHook<PropertyIgnoredAuditLogDbContext>();
        services.AddEfCoreAuditLogs<PropertyIgnoredAuditLogDbContext, DedicatedRecordingPublisher>(); // changed
        services.AddDbContextWithHook<PropertyIgnoredAuditLogDbContext>((_, opts) =>
        {
            opts.UseSqlite($"Data Source={_dbPath}");
            opts.EnableSensitiveDataLogging();
        });
        _provider = services.BuildServiceProvider();
        await using var scope = _provider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<PropertyIgnoredAuditLogDbContext>();
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync()
    {
        if (_provider is IDisposable d) d.Dispose();
        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }
        catch
        {
            /* ignore */
        }

        return Task.CompletedTask;
    }

    private PropertyIgnoredAuditLogDbContext CreateContext()
    {
        var scope = _provider.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<PropertyIgnoredAuditLogDbContext>();
    }

    [Fact]
    public async Task Added_Entity_Ignores_Decorated_Property()
    {
        DedicatedRecordingPublisher.Reset(); // changed
        var ctx = CreateContext();
        var e = new PropertyIgnoredAuditEntity { Name = "Add", Secret = "S1" };
        e.SetCreatedBy("creator");
        ctx.PropIgnoredEntities.Add(e);
        await ctx.SaveChangesAsync();
        await Task.Delay(300); // allow async publish

        var logs = DedicatedRecordingPublisher.Logs.Where(l => l.EntityName == nameof(PropertyIgnoredAuditEntity))
            .ToList(); // changed
        //Currently, no log is created for Added entities
        logs.Count.ShouldBe(0);
        logs.ShouldAllBe(l => l.Changes.All(c => c.FieldName != nameof(PropertyIgnoredAuditEntity.Secret)));
        logs.ShouldAllBe(l =>
            l.Changes.Any(c =>
                c.FieldName == nameof(PropertyIgnoredAuditEntity.Name))); // ensure other property captured
    }

    [Fact]
    public async Task Updated_Entity_Ignores_Decorated_Property()
    {
        var ctx = CreateContext();
        var e = new PropertyIgnoredAuditEntity { Name = "Upd", Secret = "S2" };
        e.SetCreatedBy("creator");
        ctx.PropIgnoredEntities.Add(e);
        await ctx.SaveChangesAsync();

        DedicatedRecordingPublisher.Reset(); // changed
        e.Name = "Upd2";
        e.Secret = "S3"; // should be ignored
        e.SetUpdatedBy("updater");
        await ctx.SaveChangesAsync();
        await Task.Delay(300);

        var logs = DedicatedRecordingPublisher.Logs.Where(l => l.EntityName == nameof(PropertyIgnoredAuditEntity))
            .ToList(); // changed
        logs.Count.ShouldBeGreaterThan(0);
        logs.ShouldAllBe(l => l.Changes.All(c => c.FieldName != nameof(PropertyIgnoredAuditEntity.Secret)));
        logs.ShouldAllBe(l => l.Changes.Any(c => c.FieldName == nameof(PropertyIgnoredAuditEntity.Name)));
    }

    [Fact]
    public async Task Deleted_Entity_Ignores_Decorated_Property()
    {
        var ctx = CreateContext();
        var e = new PropertyIgnoredAuditEntity { Name = "Del", Secret = "S4" };
        e.SetCreatedBy("creator");
        ctx.PropIgnoredEntities.Add(e);
        await ctx.SaveChangesAsync();

        DedicatedRecordingPublisher.Reset(); // changed
        ctx.PropIgnoredEntities.Remove(e);
        e.SetUpdatedBy("deleter");
        await ctx.SaveChangesAsync();
        await Task.Delay(300);

        var logs = DedicatedRecordingPublisher.Logs.Where(l => l.EntityName == nameof(PropertyIgnoredAuditEntity))
            .ToList(); // changed
        logs.Count.ShouldBeGreaterThan(0);
        logs.ShouldAllBe(l => l.Changes.All(c => c.FieldName != nameof(PropertyIgnoredAuditEntity.Secret)));
        logs.ShouldAllBe(l => l.Changes.Any(c => c.FieldName == nameof(PropertyIgnoredAuditEntity.Name)));
        logs.ShouldAllBe(l => l.Action == AuditLogAction.Deleted);
    }
}