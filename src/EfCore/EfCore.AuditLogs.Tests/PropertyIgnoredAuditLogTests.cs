using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

// added

// for AuditLogEntry & IAuditLogPublisher

namespace EfCore.AuditLogs.Tests;

public class PropertyIgnoredAuditEntity : AuditedEntity<Guid>
{
    #region Properties

    [MaxLength(200)] public string Name { get; set; } = string.Empty;

    [IgnoreAuditLog] [MaxLength(200)] public string Secret { get; set; } = string.Empty;

    #endregion

    #region Methods

    public void SetCreatedOn(string byUser, DateTimeOffset? on = null) => SetCreatedBy(byUser, on);
    public void SetUpdatedOn(string byUser, DateTimeOffset? on = null) => SetUpdatedBy(byUser, on);

    #endregion
}

// Dedicated publisher for these tests to isolate static state
internal sealed class DedicatedRecordingPublisher : IAuditLogPublisher
{
    #region Properties

    public static ConcurrentBag<AuditLogEntry> Logs { get; } = [];

    #endregion

    #region Methods

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

    #endregion
}

internal class PropertyIgnoredAuditLogDbContext(DbContextOptions<PropertyIgnoredAuditLogDbContext> options)
    : DbContext(options)
{
    #region Properties

    public DbSet<PropertyIgnoredAuditEntity> PropIgnoredEntities => Set<PropertyIgnoredAuditEntity>();

    #endregion

    #region Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<PropertyIgnoredAuditEntity>();
    }

    #endregion
}

public class PropertyIgnoredAuditLogTests : IAsyncLifetime
{
    #region Fields

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"prop_ignored_audit_{Guid.NewGuid():N}.db");
    private ServiceProvider _provider = null!;

    #endregion

    #region Methods

    [Fact]
    public async Task Added_Entity_Ignores_Decorated_Property()
    {
        DedicatedRecordingPublisher.Reset(); // changed
        var ctx = CreateContext();
        var e = new PropertyIgnoredAuditEntity { Name = "Add", Secret = "S1" };
        e.SetCreatedOn("creator");
        ctx.PropIgnoredEntities.Add(e);
        await ctx.SaveChangesAsync();
        await Task.Delay(300); // allow async publish

        var logs = DedicatedRecordingPublisher.Logs.Where(l => l.EntityName == nameof(PropertyIgnoredAuditEntity))
            .ToList(); // changed

        //Currently, no log is created for Added entities
        logs.Count.ShouldBe(1);
        logs.ShouldAllBe(l => l.Changes.Count == 0);
    }

    private PropertyIgnoredAuditLogDbContext CreateContext()
    {
        var scope = _provider.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<PropertyIgnoredAuditLogDbContext>();
    }

    [Fact]
    public async Task Deleted_Entity_Ignores_Decorated_Property()
    {
        var ctx = CreateContext();
        var e = new PropertyIgnoredAuditEntity { Name = "Del", Secret = "S4" };
        e.SetCreatedOn("creator");
        ctx.PropIgnoredEntities.Add(e);
        await ctx.SaveChangesAsync();

        DedicatedRecordingPublisher.Reset(); // changed
        ctx.PropIgnoredEntities.Remove(e);
        e.SetUpdatedOn("deleter");
        await ctx.SaveChangesAsync();
        await Task.Delay(300);

        var logs = DedicatedRecordingPublisher.Logs.Where(l => l.EntityName == nameof(PropertyIgnoredAuditEntity))
            .ToList(); // changed
        logs.Count(e => e.Action == AuditLogAction.Deleted).ShouldBeGreaterThan(0);
        logs.Where(e => e.Action == AuditLogAction.Deleted).All(l => l.Changes.Count > 0).ShouldBeTrue();
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

    [Fact]
    public async Task Updated_Entity_Ignores_Decorated_Property()
    {
        var ctx = CreateContext();
        var e = new PropertyIgnoredAuditEntity { Name = "Upd", Secret = "S2" };
        e.SetCreatedOn("creator");
        ctx.PropIgnoredEntities.Add(e);
        await ctx.SaveChangesAsync();

        DedicatedRecordingPublisher.Reset(); // changed
        e.Name = "Upd2";
        e.Secret = "S3"; // should be ignored
        e.SetUpdatedOn("updater");
        await ctx.SaveChangesAsync();
        await Task.Delay(300);

        var logs = DedicatedRecordingPublisher.Logs.Where(l => l.EntityName == nameof(PropertyIgnoredAuditEntity))
            .ToList(); // changed

        logs.Count.ShouldBeGreaterThan(0);

        logs.Where(l => l.Action != AuditLogAction.Created).ShouldAllBe(l =>
            l.Changes.All(c => !c.FieldName.Equals(nameof(PropertyIgnoredAuditEntity.Secret))));

        logs.Where(l => l.Action != AuditLogAction.Created).ShouldAllBe(l =>
            l.Changes.Any(c => c.FieldName.Equals(nameof(PropertyIgnoredAuditEntity.Name))));
    }

    #endregion
}