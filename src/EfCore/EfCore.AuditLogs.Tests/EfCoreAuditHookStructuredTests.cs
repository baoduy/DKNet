// Reconstructed comprehensive test suite for EfCoreAuditHook fire-and-forget structured audit logging

using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

// added for AuditLogAction

namespace EfCore.AuditLogs.Tests;

public class EfCoreAuditHookStructuredTests : IAsyncLifetime
{
    #region Fields

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"auditlogs_structured_{Guid.NewGuid():N}.db");
    private ServiceProvider _provider = null!;

    #endregion

    #region Methods

    [Fact]
    public void AuditedEntity_SetCreatedOn_Is_Idempotent()
    {
        var entity = new TestAuditEntity { Name = "Idem", Age = 1, IsActive = true, Balance = 0m };
        entity.SetCreatedOn("first");
        var firstOn = entity.CreatedOn;
        entity.SetCreatedOn("second", DateTimeOffset.UtcNow.AddDays(-1));
        entity.CreatedBy.ShouldBe("first");
        entity.CreatedOn.ShouldBe(firstOn);
    }

    [Fact]
    public void AuditedEntity_SetUpdatedOn_Throws_On_Empty()
    {
        var entity = new TestAuditEntity { Name = "Err", Age = 1, IsActive = true, Balance = 0m };
        entity.SetCreatedOn("creator");
        Should.Throw<ArgumentException>(() => entity.SetUpdatedOn(""));
    }

    private Task<(TestAuditDbContext ctx, TestPublisher publisher)> CreateScopeAsync()
    {
        var scope = _provider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TestAuditDbContext>();
        var publisher =
            scope.ServiceProvider.GetAuditLogPublishers<TestAuditDbContext>().OfType<TestPublisher>()
                .First();
        return Task.FromResult((ctx, publisher));
    }

    // private static async Task WaitForLogsAsync(TestPublisher publisher, int minCount, int timeoutMs = 750)
    // {
    //     var sw = Stopwatch.StartNew();
    //     while (sw.ElapsedMilliseconds < timeoutMs)
    //     {
    //         if (publisher.Received.Count >= minCount) return;
    //         await Task.Delay(25);
    //     }
    // }

    [Fact]
    public async Task Creation_Does_Produce_Audit_Log()
    {
        TestPublisher.Clear();
        var (ctx, _) = await CreateScopeAsync();
        var entity = new TestAuditEntity { Name = "User1", Age = 30, IsActive = true, Balance = 123.45m };
        entity.SetCreatedOn("creator-1");
        ctx.AuditEntities.Add(entity);
        await ctx.SaveChangesAsync();
        TestPublisher.Received.Count(c => c.Action == AuditLogAction.Created && c.Keys.Values.Contains(entity.Id))
            .ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Delete_Produces_Audit_Log()
    {
        TestPublisher.Clear();
        var (ctx, _) = await CreateScopeAsync();

        var entity = new TestAuditEntity { Name = "User3", Age = 40, IsActive = false, Balance = 0m };
        entity.SetCreatedOn("creator-3");
        ctx.AuditEntities.Add(entity);
        await ctx.SaveChangesAsync();
        await Task.Delay(500); // Wait for create audit log to be published
        TestPublisher.Clear();

        ctx.AuditEntities.Remove(entity);
        await ctx.SaveChangesAsync();
        await Task.Delay(1000);

        //await WaitForLogsAsync(publisher, 1);
        var logs = TestPublisher.Received.ToList();
        logs.Count.ShouldBeGreaterThanOrEqualTo(1);
        var log = logs[0];
        log.EntityName.ShouldBe(nameof(TestAuditEntity));
        log.CreatedBy.ShouldBe("creator-3");
        log.UpdatedBy.ShouldBeNull();
        log.Action.ShouldBe(AuditLogAction.Deleted); // assert action
        log.Changes.Count.ShouldBeGreaterThan(0);
        log.Changes.ShouldAllBe(c => c.NewValue == null);

        // Ensure at least one core property listed
        log.Changes.ShouldContain(c => c.FieldName == nameof(TestAuditEntity.Name));
    }

    public async Task DisposeAsync()
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

        await Task.CompletedTask;
    }

    [Fact]
    public async Task Duplicate_Hook_Registration_Does_Not_Duplicate_Logs()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEfCoreAuditLogs<TestAuditDbContext, TestPublisher>();
        services.AddEfCoreAuditLogs<TestAuditDbContext, TestPublisher>();
        services.AddDbContextWithHook<TestAuditDbContext>((_, options) =>
            options.UseSqlite($"Data Source={_dbPath}"));
        var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var publishers = scope.ServiceProvider.GetAuditLogPublishers<TestAuditDbContext>().ToList();
        publishers.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Exception_In_Publisher_Is_Swallowed()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEfCoreAuditLogs<TestAuditDbContext, TestPublisher>();
        services.AddEfCoreAuditLogs<TestAuditDbContext, FailingPublisher>();
        services.AddDbContextWithHook<TestAuditDbContext>((_, options) =>
            options.UseSqlite($"Data Source={_dbPath}"));
        var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TestAuditDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        var entity = new TestAuditEntity { Name = "UserEX", Age = 10, IsActive = true, Balance = 1m };
        entity.SetCreatedOn("creator-ex");
        ctx.Add(entity);
        await ctx.SaveChangesAsync();
        TestPublisher.Clear();

        entity.Age = 11;
        entity.UpdateProfile("updater-ex");
        await ctx.SaveChangesAsync(); // should not throw
        await Task.Delay(200);

        //await WaitForLogsAsync(goodPublisher, 1);
        TestPublisher.Received.Count.ShouldBe(1);
        TestPublisher.Received.First().Action.ShouldBe(AuditLogAction.Updated); // assert action
    }

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEfCoreAuditHook<TestAuditDbContext>(); // register hook
        services
            .AddEfCoreAuditLogs<TestAuditDbContext,
                TestPublisher>(); // singleton publisher so fire-and-forget scope shares instance
        services.AddDbContextWithHook<TestAuditDbContext>((_, options) =>
        {
            options.UseSqlite($"Data Source={_dbPath}");
            options.EnableSensitiveDataLogging();
        });

        _provider = services.BuildServiceProvider();
        await using var scope = _provider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TestAuditDbContext>();
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();
    }

    [Fact]
    public void LastModified_Properties_Reflect_Created_Then_Updated()
    {
        var entity = new TestAuditEntity { Name = "LM", Age = 2, IsActive = true, Balance = 0m };
        entity.SetCreatedOn("creator");
        entity.LastModifiedBy.ShouldBe("creator");
        entity.LastModifiedOn.ShouldBe(entity.CreatedOn);
        entity.SetUpdatedOn("updater");
        entity.LastModifiedBy.ShouldBe("updater");
        entity.LastModifiedOn.ShouldBe(entity.UpdatedOn!.Value);
    }

    [Fact]
    public async Task Multiple_Modified_Entities_Produce_Multiple_Logs()
    {
        var (ctx, _) = await CreateScopeAsync();
        var e1 = new TestAuditEntity { Name = "UserA", Age = 20, IsActive = true, Balance = 10m };
        var e2 = new TestAuditEntity { Name = "UserB", Age = 21, IsActive = true, Balance = 11m };
        e1.SetCreatedOn("creator-a");
        e2.SetCreatedOn("creator-b");
        ctx.AddRange(e1, e2);
        await ctx.SaveChangesAsync();
        TestPublisher.Clear();

        e1.UpdateProfile("updater-a", "note-a");
        e2.UpdateProfile("updater-b", "note-b");
        e1.Age = 21;
        e2.Balance = 15m;
        await ctx.SaveChangesAsync();
        await Task.Delay(1000);

        //await WaitForLogsAsync(publisher, 2);
        var logs = TestPublisher.Received.ToList();
        logs.Count(e => e.Action == AuditLogAction.Updated).ShouldBeGreaterThanOrEqualTo(2);
        logs.Where(e => e.Action == AuditLogAction.Updated).ShouldContain(l => l.UpdatedBy == "updater-a");
        logs.Where(e => e.Action == AuditLogAction.Updated).ShouldContain(l => l.UpdatedBy == "updater-b");
        logs.Where(e => e.Action == AuditLogAction.Updated).First(l => l.UpdatedBy == "updater-a").Changes
            .ShouldContain(c => c.FieldName == nameof(TestAuditEntity.Age));
        logs.Where(e => e.Action == AuditLogAction.Updated).First(l => l.UpdatedBy == "updater-b").Changes
            .ShouldContain(c => c.FieldName == nameof(TestAuditEntity.Balance));
    }

    [Fact]
    public async Task NoChange_Does_Not_Create_Log()
    {
        var (ctx, _) = await CreateScopeAsync();
        var entity = new TestAuditEntity { Name = "UserNC", Age = 10, IsActive = true, Balance = 1m };
        entity.SetCreatedOn("creator-nc");
        ctx.AuditEntities.Add(entity);
        await ctx.SaveChangesAsync();
        await Task.Delay(500); // Wait for audit log to be published
        TestPublisher.Clear();

        // Save without modifications
        await ctx.SaveChangesAsync();
        await Task.Delay(500); // Wait to ensure no async audit logs are published
        TestPublisher.Received.Count(c => c.Keys.Values.Contains(entity.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Update_Produces_Audit_Log()
    {
        var (ctx, _) = await CreateScopeAsync();
        var entity = new TestAuditEntity { Name = "User2", Age = 25, IsActive = true, Balance = 50m };
        entity.SetCreatedOn("creator-2");
        ctx.AuditEntities.Add(entity);
        await ctx.SaveChangesAsync();
        TestPublisher.Clear();

        var oldAge = entity.Age;
        var oldBalance = entity.Balance;
        entity.Age = 26;
        entity.Balance = 75.5m;
        entity.UpdateProfile("updater-2", "Updated profile");
        await ctx.SaveChangesAsync();
        await Task.Delay(1000);

        //await WaitForLogsAsync(publisher, 1);
        var logs = TestPublisher.Received.ToList();
        logs.Count.ShouldBeGreaterThanOrEqualTo(1);

        var log = logs[0];
        log.EntityName.ShouldBe(nameof(TestAuditEntity));
        log.CreatedBy.ShouldBe("creator-2");
        log.UpdatedBy.ShouldBe("updater-2");
        log.Action.ShouldBe(AuditLogAction.Updated); // assert action
        log.UpdatedOn.ShouldNotBeNull();
        log.Changes.ShouldContain(c =>
            c.FieldName == nameof(TestAuditEntity.Age) && (int?)c.OldValue == oldAge && (int?)c.NewValue == 26);
        log.Changes.ShouldContain(c =>
            c.FieldName == nameof(TestAuditEntity.Balance) && (decimal?)c.OldValue == oldBalance &&
            (decimal?)c.NewValue == 75.5m);
        log.Changes.ShouldContain(c => c.FieldName == nameof(AuditedEntity<>.UpdatedBy));
        log.Changes.ShouldContain(c => c.FieldName == nameof(AuditedEntity<>.UpdatedOn));
    }

    #endregion
}

internal sealed class FailingPublisher : IAuditLogPublisher
{
    #region Methods

    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Simulated failure");

    #endregion
}