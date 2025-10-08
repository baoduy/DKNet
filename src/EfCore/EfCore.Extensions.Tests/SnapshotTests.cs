using DKNet.EfCore.Extensions.Snapshots;

namespace EfCore.Extensions.Tests;

public class SnapshotTests(MemoryFixture fixture) : IClassFixture<MemoryFixture>
{
    private readonly MyDbContext _db = fixture.Db!;


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
    public async Task SnapshotEntities_ShouldCaptureChangedEntities()
    {
        // Arrange
        var user = new User("Test Creator") { FirstName = "Test", LastName = "User" };
        _db.Set<User>().Add(user);

        // Act
        await using var snapshot = new SnapshotContext(_db);
        var snapshotEntities = snapshot.Entities;

        // Assert
        snapshotEntities.ShouldNotBeEmpty();
        snapshotEntities.ShouldContain(e => e.Entity == user);
    }

    [Fact]
    public async Task SnapshotEntities_MultipleAccess_ShouldReturnSameInstance()
    {
        // Arrange
        await using var snapshot = new SnapshotContext(_db);

        // Act
        var firstAccess = snapshot.Entities;
        var secondAccess = snapshot.Entities;

        // Assert
        firstAccess.ShouldBeSameAs(secondAccess);
    }

    [Fact]
    public async Task SnapshotEntityEntry_ShouldCaptureEntityState()
    {
        // Arrange
        var user = new User("Test Creator") { FirstName = "Test", LastName = "User" };
        _db.Set<User>().Add(user);

        await using var snapshot = new SnapshotContext(_db);
        var entry = snapshot.Entities[0];

        // Assert
        entry.Entity.ShouldBeOfType<User>();
        entry.OriginalState.ShouldBe(EntityState.Added);
    }
}