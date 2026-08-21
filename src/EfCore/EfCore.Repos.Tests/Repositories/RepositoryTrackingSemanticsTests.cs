namespace EfCore.Repos.Tests.Repositories;

/// <summary>
///     Pins the tracking-semantics contract of the read-surface refactor (DRK-328): <see cref="Repository{TEntity}" />
///     .Query() is TRACKED and its inherited read helpers (CountAsync/ExistsAsync/FindAsync/Query(filter)) dispatch
///     through that tracked queryable, while <see cref="ReadRepository{TEntity}" />.Query() stays
///     <c>AsNoTracking()</c> and mutations against it never reach the database.
/// </summary>
public class RepositoryTrackingSemanticsTests(RepositoryFixture fixture) : IClassFixture<RepositoryFixture>
{
    #region Methods

    [Fact]
    public async Task EntryReturnsEntityEntryForTrackedEntity()
    {
        fixture.DbContext!.ChangeTracker.Clear();

        // Arrange
        var entity = new User("entrytest1") { FirstName = "Entry", LastName = "Test" };
        await fixture.Repository.AddAsync(entity);
        await fixture.Repository.SaveChangesAsync();

        // Act
        var entry = fixture.Repository.Entry(entity);

        // Assert
        entry.ShouldNotBeNull();
        entry.Entity.ShouldBe(entity);
    }

    [Fact]
    public async Task ReadRepositoryQueryMaterializedEntityMutationIsNotPersistedOnSaveChanges()
    {
        fixture.DbContext!.ChangeTracker.Clear();

        // Arrange: seed a row, then detach everything so the next read is a fresh materialization
        var entity = new User("tracksave2") { FirstName = "Before", LastName = "Test" };
        fixture.DbContext.Add(entity);
        await fixture.DbContext.SaveChangesAsync();
        fixture.DbContext.ChangeTracker.Clear();

        // Act: read through ReadRepository (AsNoTracking), mutate the detached instance, then save
        var untracked = await fixture.ReadRepository.Query().FirstAsync(u => u.Id == entity.Id);
        fixture.DbContext.Entry(untracked).State.ShouldBe(EntityState.Detached);
        untracked.FirstName = "After";

        var affected = await fixture.Repository.SaveChangesAsync();

        // Assert: nothing was tracked, so nothing was written back
        affected.ShouldBe(0);
        var reloaded = await fixture.DbContext.Set<User>().FindAsync(entity.Id);
        reloaded!.FirstName.ShouldBe("Before");
    }

    [Fact]
    public async Task RepositoryCountAsyncDispatchesThroughTrackedQueryAndCountsMatches()
    {
        fixture.DbContext!.ChangeTracker.Clear();

        // Arrange
        var entities = new[]
        {
            new User("counttest1") { FirstName = "CountMe", LastName = "Test" },
            new User("counttest2") { FirstName = "CountMe", LastName = "Test" }
        };
        fixture.DbContext.AddRange(entities);
        await fixture.DbContext.SaveChangesAsync();

        // Act
        var count = await fixture.Repository.CountAsync(u => u.FirstName == "CountMe");

        // Assert
        count.ShouldBe(2);
    }

    [Fact]
    public async Task RepositoryExistsAsyncReturnsFalseWhenNoEntityMatches()
    {
        // Act
        var exists = await fixture.Repository.ExistsAsync(u => u.FirstName == "NoSuchUser");

        // Assert
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task RepositoryExistsAsyncReturnsTrueWhenEntityMatches()
    {
        fixture.DbContext!.ChangeTracker.Clear();

        // Arrange
        var entity = new User("existstest1") { FirstName = "ExistsMe", LastName = "Test" };
        fixture.DbContext.Add(entity);
        await fixture.DbContext.SaveChangesAsync();

        // Act
        var exists = await fixture.Repository.ExistsAsync(u => u.FirstName == "ExistsMe");

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task RepositoryFindAsyncWithFilterReturnsTrackedEntity()
    {
        fixture.DbContext!.ChangeTracker.Clear();

        // Arrange
        var entity = new User("findfiltertrack1") { FirstName = "FindFilterTrack", LastName = "Test" };
        fixture.DbContext.Add(entity);
        await fixture.DbContext.SaveChangesAsync();
        fixture.DbContext.ChangeTracker.Clear();

        // Act
        var result = await fixture.Repository.FindAsync(u => u.FirstName == "FindFilterTrack");

        // Assert
        result.ShouldNotBeNull();
        fixture.DbContext.Entry(result).State.ShouldNotBe(EntityState.Detached);
    }

    [Fact]
    public async Task RepositoryQueryMaterializedEntityMutationIsPersistedOnSaveChanges()
    {
        fixture.DbContext!.ChangeTracker.Clear();

        // Arrange: seed a row, then detach everything so the next read is a fresh materialization
        var entity = new User("tracksave1") { FirstName = "Before", LastName = "Test" };
        fixture.DbContext.Add(entity);
        await fixture.DbContext.SaveChangesAsync();
        fixture.DbContext.ChangeTracker.Clear();

        // Act: read through Repository (tracked), mutate the materialized instance, then save
        var tracked = await fixture.Repository.Query().FirstAsync(u => u.Id == entity.Id);
        fixture.DbContext.Entry(tracked).State.ShouldNotBe(EntityState.Detached);
        tracked.FirstName = "After";

        var affected = await fixture.Repository.SaveChangesAsync();

        // Assert: the mutation on the tracked instance was written back
        affected.ShouldBe(1);
        fixture.DbContext.ChangeTracker.Clear();
        var reloaded = await fixture.DbContext.Set<User>().FindAsync(entity.Id);
        reloaded!.FirstName.ShouldBe("After");
    }

    [Fact]
    public async Task RepositoryQueryWithFilterReturnsTrackedQueryable()
    {
        fixture.DbContext!.ChangeTracker.Clear();

        // Arrange
        var entity = new User("queryfiltertrack1") { FirstName = "FilterTrack", LastName = "Test" };
        fixture.DbContext.Add(entity);
        await fixture.DbContext.SaveChangesAsync();
        fixture.DbContext.ChangeTracker.Clear();

        // Act
        var result = await fixture.Repository.Query(u => u.FirstName == "FilterTrack").FirstAsync();

        // Assert
        fixture.DbContext.Entry(result).State.ShouldNotBe(EntityState.Detached);
    }

    #endregion
}
