using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace EfCore.AuditLogs.Tests;

// Entity decorated with IgnoreAuditLogAttribute should never produce audit logs
[IgnoreAuditLog]
internal class IgnoredAuditEntity : AuditedEntity<Guid>
{
    #region Properties

    [MaxLength(200)] public string Name { get; set; } = string.Empty;

    public int Value { get; set; }

    #endregion

    #region Methods

    public void SetCreatedOn(string byUser, DateTimeOffset? on = null) => SetCreatedBy(byUser, on);
    public void SetUpdatedOn(string byUser, DateTimeOffset? on = null) => SetUpdatedBy(byUser, on);

    #endregion
}

public sealed class TestIgnoredPublisher : IAuditLogPublisher
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

internal class IgnoredAuditLogDbContext(DbContextOptions<IgnoredAuditLogDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<IgnoredAuditEntity> IgnoredEntities => Set<IgnoredAuditEntity>();

    #endregion

    #region Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<IgnoredAuditEntity>();
    }

    #endregion
}

public class IgnoredAuditLogTests : IAsyncLifetime
{
    #region Fields

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ignored_audit_{Guid.NewGuid():N}.db");
    private ServiceProvider _provider = null!;

    #endregion

    #region Methods

    [Fact]
    public async Task Added_Entity_With_IgnoreAttribute_Produces_No_Log()
    {
        TestIgnoredPublisher.Clear();
        var ctx = CreateContext();
        var e = new IgnoredAuditEntity { Name = "Add", Value = 1 };
        e.SetCreatedOn("creator");
        ctx.IgnoredEntities.Add(e);
        await ctx.SaveChangesAsync();
        await Task.Delay(150);
        TestIgnoredPublisher.Received.ShouldBeEmpty();
    }

    private IgnoredAuditLogDbContext CreateContext()
    {
        var scope = _provider.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<IgnoredAuditLogDbContext>();
    }

    [Fact]
    public async Task Deleted_Entity_With_IgnoreAttribute_Produces_No_Log()
    {
        var ctx = CreateContext();
        var e = new IgnoredAuditEntity { Name = "Del", Value = 5 };
        e.SetCreatedOn("creator");
        ctx.IgnoredEntities.Add(e);
        await ctx.SaveChangesAsync();
        TestIgnoredPublisher.Clear();
        ctx.IgnoredEntities.Remove(e);
        await ctx.SaveChangesAsync();
        await Task.Delay(150);
        TestIgnoredPublisher.Received.ShouldBeEmpty();
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
        services.AddEfCoreAuditHook<IgnoredAuditLogDbContext>();
        services.AddEfCoreAuditLogs<IgnoredAuditLogDbContext, TestIgnoredPublisher>();
        services.AddDbContextWithHook<IgnoredAuditLogDbContext>((_, opts) =>
        {
            opts.UseSqlite($"Data Source={_dbPath}");
            opts.EnableSensitiveDataLogging();
        });
        _provider = services.BuildServiceProvider();
        await using var scope = _provider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IgnoredAuditLogDbContext>();
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task Updated_Entity_With_IgnoreAttribute_Produces_No_Log()
    {
        var ctx = CreateContext();
        var e = new IgnoredAuditEntity { Name = "Upd", Value = 2 };
        e.SetCreatedOn("creator");
        ctx.IgnoredEntities.Add(e);
        await ctx.SaveChangesAsync();
        TestIgnoredPublisher.Clear();
        e.Value = 3;
        e.SetUpdatedOn("updater");
        await ctx.SaveChangesAsync();
        await Task.Delay(150);
        TestIgnoredPublisher.Received.ShouldBeEmpty();
    }

    #endregion
}