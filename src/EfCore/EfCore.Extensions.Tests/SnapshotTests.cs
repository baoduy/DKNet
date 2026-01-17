using DKNet.EfCore.Extensions.Snapshots;

namespace EfCore.Extensions.Tests;

public class SnapshotTests(MemoryFixture fixture) : IClassFixture<MemoryFixture>
{
    #region Fields

    private readonly MyDbContext _db = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public async Task Snapshot_ShouldCreateSnapshotContext()
    {
        // Act
        await using var snapshot = new SnapshotContext(_db);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.DbContext.ShouldBe(_db);
    }

    [Fact]
    public async Task SnapshotContext_Dispose_ShouldReleaseResources()
    {
        // Arrange
        var snapshot = new SnapshotContext(_db);

        // Act
        await snapshot.DisposeAsync();

        // Assert
        Should.Throw<ObjectDisposedException>(() => snapshot.DbContext);
    }

    [Fact]
    public async Task SnapshotEntities_MultipleAccess_ShouldReturnSameInstance()
    {
        // Arrange
        await using var snapshot = new SnapshotContext(_db);
        snapshot.Initialize();

        // Act
        var firstAccess = snapshot.Entities;
        var secondAccess = snapshot.Entities;

        // Assert
        firstAccess.ShouldBeSameAs(secondAccess);
    }

    [Fact]
    public async Task SnapshotEntities_ShouldCaptureChangedEntities()
    {
        // Arrange
        var user = new User("Test Creator") { FirstName = "Test", LastName = "User" };
        _db.Set<User>().Add(user);

        // Act
        await using var snapshot = new SnapshotContext(_db);
        snapshot.Initialize();

        var snapshotEntities = snapshot.Entities;

        // Assert
        snapshotEntities.ShouldNotBeEmpty();
        snapshotEntities.ShouldContain(e => e.Entity == user);
    }

    [Fact]
    public async Task SnapshotEntityEntry_ShouldCaptureEntityState()
    {
        // Arrange
        var user = new User("Test Creator") { FirstName = "Test", LastName = "User" };
        _db.Set<User>().Add(user);

        await using var snapshot = new SnapshotContext(_db);
        snapshot.Initialize();

        var entry = snapshot.Entities.First();

        // Assert
        entry.Entity.ShouldBeOfType<User>();
        entry.OriginalState.ShouldBe(EntityState.Added);
    }

    #endregion
}