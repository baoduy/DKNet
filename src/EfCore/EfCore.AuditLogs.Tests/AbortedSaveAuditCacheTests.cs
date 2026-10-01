// Acceptance tests for DRK-1964: a save that does not finish (cancelled, or failing with DbUpdateException)
// must leave no pending audit entry behind, so the next save on the same DbContext publishes only its own
// entries. Scenarios S1–S3 and their literal counts are copied from DRK-1968 §7 (frozen).

using System.Reflection;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace EfCore.AuditLogs.Tests;

public class AbortedSaveAuditCacheTests
{
    #region Helpers

    private static string NewDbPath() =>
        Path.Combine(Path.GetTempPath(), $"audit_aborted_save_{Guid.NewGuid():N}.db");

    private static async
        Task<(ServiceProvider Root, AsyncServiceScope Scope, TestAuditDbContext Db, TestPublisher Publisher)>
        BuildAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEfCoreAuditLogs<TestAuditDbContext, TestPublisher>();
        services.AddDbContextWithHook<TestAuditDbContext>((_, options) =>
            options.UseSqlite($"Data Source={NewDbPath()}"));

        var root = services.BuildServiceProvider();
        var scope = root.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TestAuditDbContext>();
        var publisher = scope.ServiceProvider.GetAuditLogPublishers<TestAuditDbContext>()
            .OfType<TestPublisher>().First();
        await db.Database.EnsureCreatedAsync();
        return (root, scope, db, publisher);
    }

    private static TestAuditEntity NewAudited(string name)
    {
        var entity = new TestAuditEntity { Name = name, Age = 1, IsActive = true, Balance = 1m };
        entity.SetCreatedOn("creator");
        return entity;
    }

    // Entity<TKey>.Id has a private setter; the duplicate-key scenario needs a second instance with a known id.
    private static void SetId(TestAuditEntity entity, Guid id) =>
        typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(entity, id);

    #endregion

    #region Tests

    [Fact]
    public async Task S1_CancelledSave_ThenPlainSave_PublishesNoEntryOfTheCancelledSave()
    {
        var (root, scope, db, publisher) = await BuildAsync();
        await using var _ = root;
        await using var __ = scope;

        // Given an audited entity whose save is cancelled
        var cancelled = NewAudited("Cancelled");
        db.Add(cancelled);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => db.SaveChangesAsync(cts.Token));

        // When that entity is detached and a plain entity is saved on the same DbContext
        db.Entry(cancelled).State = EntityState.Detached;
        db.Add(new PlainEntity { Name = "Plain" });
        await db.SaveChangesAsync();

        // Then no TestAuditEntity entry is published and no TestAuditEntity row exists
        publisher.Received.Count(e => e.EntityName == "TestAuditEntity").ShouldBe(0);
        (await db.AuditEntities.AsNoTracking().CountAsync()).ShouldBe(0);
        (await db.PlainEntities.AsNoTracking().CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task S2_DuplicateKeySave_ThenPlainSave_PublishesNoEntryOfTheFailedSave()
    {
        var (root, scope, db, publisher) = await BuildAsync();
        await using var _ = root;
        await using var __ = scope;

        // Given a TestAuditEntity row already stored under a fixed id
        var seed = NewAudited("Seed");
        db.Add(seed);
        await db.SaveChangesAsync();
        var seedId = seed.Id;
        db.ChangeTracker.Clear();
        publisher.Clear();

        // And a save of a second TestAuditEntity with the same id fails with DbUpdateException
        var duplicate = NewAudited("Duplicate");
        SetId(duplicate, seedId);
        db.Add(duplicate);
        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // When that entity is detached and a plain entity is saved on the same DbContext
        db.Entry(duplicate).State = EntityState.Detached;
        db.Add(new PlainEntity { Name = "Plain" });
        await db.SaveChangesAsync();

        // Then no TestAuditEntity entry is published and the failed row was never written
        publisher.Received.Count(e => e.EntityName == "TestAuditEntity").ShouldBe(0);
        (await db.AuditEntities.AsNoTracking().CountAsync(e => e.Name == "Duplicate")).ShouldBe(0);
        var stored = await db.AuditEntities.AsNoTracking().Where(e => e.Id == seedId).ToListAsync();
        stored.Count.ShouldBe(1);
        stored[0].Name.ShouldBe("Seed");
    }

    [Fact]
    public async Task S3_FailedAuditedSave_ThenAuditedSaveOfB_PublishesExactlyBsEntry()
    {
        var (root, scope, db, publisher) = await BuildAsync();
        await using var _ = root;
        await using var __ = scope;

        // Given an audited save of entity A that is cancelled
        var entityA = NewAudited("A");
        db.Add(entityA);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => db.SaveChangesAsync(cts.Token));
        db.Entry(entityA).State = EntityState.Detached;

        // When an audited entity B is saved successfully on the same DbContext
        var entityB = NewAudited("B");
        db.Add(entityB);
        await db.SaveChangesAsync();

        // Then exactly one TestAuditEntity entry is published, it is B's, and none is A's
        var published = publisher.Received.Where(e => e.EntityName == "TestAuditEntity").ToList();
        published.Count.ShouldBe(1);
        published[0].Keys.Values.ShouldContain(entityB.Id);
        published.ShouldNotContain(e => e.Keys.Values.Contains(entityA.Id));
    }

    #endregion
}
