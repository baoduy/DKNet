// Acceptance tests for DRK-1425: TestPublisher must collect audit entries per DI-scoped instance instead of
// through one assembly-wide static bag, so concurrent xUnit test classes stop reading and clearing each
// other's audit entries. Scenarios and literal names are copied verbatim from DRK-1425 §7 (frozen).

using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace EfCore.AuditLogs.Tests;

public class AuditPublisherIsolationTests
{
    #region Helpers

    private static string NewDbPath() =>
        Path.Combine(Path.GetTempPath(), $"audit_publisher_isolation_{Guid.NewGuid():N}.db");

    private static async
        Task<(ServiceProvider Root, IServiceScope Scope, TestAuditDbContext Db, TestPublisher Publisher)>
        BuildAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEfCoreAuditLogs<TestAuditDbContext, TestPublisher>();
        services.AddDbContextWithHook<TestAuditDbContext>((_, options) =>
            options.UseSqlite($"Data Source={NewDbPath()}"));

        var root = services.BuildServiceProvider();
        var scope = root.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestAuditDbContext>();
        var publisher = scope.ServiceProvider.GetAuditLogPublishers<TestAuditDbContext>()
            .OfType<TestPublisher>().First();
        await db.Database.EnsureCreatedAsync();
        return (root, scope, db, publisher);
    }

    #endregion

    #region Tests

    [Fact]
    public async Task Publisher_Instances_Do_Not_Share_Entries()
    {
        // Given two independently built providers, each registering TestPublisher for its own DbContext scope
        var (rootA, scopeA, dbA, publisherA) = await BuildAsync();
        await using var rootADisposable = rootA;
        using var scopeADisposable = scopeA;
        var (rootB, scopeB, dbB, publisherB) = await BuildAsync();
        await using var rootBDisposable = rootB;
        using var scopeBDisposable = scopeB;

        // When an entity is saved through each
        var entityA = new TestAuditEntity { Name = "Isolation-A", Age = 1, IsActive = true, Balance = 0m };
        entityA.SetCreatedOn("creator-a");
        dbA.AuditEntities.Add(entityA);
        await dbA.SaveChangesAsync();

        var entityB = new TestAuditEntity { Name = "Isolation-B", Age = 2, IsActive = true, Balance = 0m };
        entityB.SetCreatedOn("creator-b");
        dbB.AuditEntities.Add(entityB);
        await dbB.SaveChangesAsync();

        // Then each resolved publisher's collected entries contain only its own entity's key, and the other's
        // key is absent
        publisherA.Received.ShouldContain(e => e.Keys.Values.Contains(entityA.Id));
        publisherA.Received.ShouldNotContain(e => e.Keys.Values.Contains(entityB.Id));
        publisherB.Received.ShouldContain(e => e.Keys.Values.Contains(entityB.Id));
        publisherB.Received.ShouldNotContain(e => e.Keys.Values.Contains(entityA.Id));
    }

    [Fact]
    public async Task Clearing_One_Publisher_Leaves_Another_Intact()
    {
        // Given two publisher instances that have each collected at least one entry
        var (rootA, scopeA, dbA, publisherA) = await BuildAsync();
        await using var rootADisposable = rootA;
        using var scopeADisposable = scopeA;
        var (rootB, scopeB, dbB, publisherB) = await BuildAsync();
        await using var rootBDisposable = rootB;
        using var scopeBDisposable = scopeB;

        var entityA = new TestAuditEntity { Name = "Clear-A", Age = 3, IsActive = true, Balance = 0m };
        entityA.SetCreatedOn("creator-clear-a");
        dbA.AuditEntities.Add(entityA);
        await dbA.SaveChangesAsync();

        var entityB = new TestAuditEntity { Name = "Clear-B", Age = 4, IsActive = true, Balance = 0m };
        entityB.SetCreatedOn("creator-clear-b");
        dbB.AuditEntities.Add(entityB);
        await dbB.SaveChangesAsync();

        // When one is cleared
        publisherA.Clear();

        // Then the other still reports its entry
        publisherA.Received.ShouldNotContain(e => e.Keys.Values.Contains(entityA.Id));
        publisherB.Received.ShouldContain(e => e.Keys.Values.Contains(entityB.Id));
    }

    [Fact]
    public async Task Published_Entry_Is_Visible_Without_Waiting()
    {
        // Given one provider and scope
        var (root, scope, db, publisher) = await BuildAsync();
        await using var rootDisposable = root;
        using var scopeDisposable = scope;
        publisher.Clear();

        // When an entity is saved
        var entity = new TestAuditEntity { Name = "NoWait", Age = 5, IsActive = true, Balance = 0m };
        entity.SetCreatedOn("creator-no-wait");
        db.AuditEntities.Add(entity);
        await db.SaveChangesAsync();

        // Then the resolved publisher reports the entry immediately after SaveChangesAsync returns, with no
        // Task.Delay anywhere in this test
        publisher.Received.ShouldContain(e => e.Keys.Values.Contains(entity.Id));
    }

    #endregion
}
