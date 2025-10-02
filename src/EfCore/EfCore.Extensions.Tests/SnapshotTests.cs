using DKNet.EfCore.Extensions.Snapshots;

namespace EfCore.Extensions.Tests;

public class SnapshotTests(MemoryFixture fixture) : IClassFixture<MemoryFixture>
{
    private readonly MyDbContext _db = fixture.Db!;


    [Fact]
    public void Snapshot_ShouldCreateSnapshotContext()
    {
        // Act
        using var snapshot = new SnapshotContext(_db);

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
    public void SnapshotEntities_ShouldCaptureChangedEntities()
    {
        // Arrange
        var user = new User("Test Creator") { FirstName = "Test", LastName = "User" };
        _db.Set<User>().Add(user);

        // Act
        using var snapshot = new SnapshotContext(_db);
        var snapshotEntities = snapshot.SnapshotEntities;

        // Assert
        snapshotEntities.ShouldNotBeEmpty();
        snapshotEntities.ShouldContain(e => e.Entity == user);
    }

    [Fact]
    public void SnapshotEntities_MultipleAccess_ShouldReturnSameInstance()
    {
        // Arrange
        using var snapshot = new SnapshotContext(_db);

        // Act
        var firstAccess = snapshot.SnapshotEntities;
        var secondAccess = snapshot.SnapshotEntities;

        // Assert
        firstAccess.ShouldBeSameAs(secondAccess);
    }

    [Fact]
    public void SnapshotEntityEntry_ShouldCaptureEntityState()
    {
        // Arrange
        var user = new User("Test Creator") { FirstName = "Test", LastName = "User" };
        _db.Set<User>().Add(user);

        using var snapshot = new SnapshotContext(_db);
        var entry = snapshot.SnapshotEntities[0];

        // Assert
        entry.Entity.ShouldBeOfType<User>();
        entry.OriginalState.ShouldBe(EntityState.Added);
    }
}