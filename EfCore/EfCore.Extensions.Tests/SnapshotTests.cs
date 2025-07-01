namespace EfCore.Extensions.Tests;


public class SnapshotTests : SqlServerTestBase
{
    private static MyDbContext _db;

    
    public static async Task ClassSetup()
    {
        await StartSqlContainerAsync();
        _db = CreateDbContext("EventDb");
        await _db.Database.EnsureCreatedAsync();
    }

    [Fact]
    public void Snapshot_ShouldCreateSnapshotContext()
    {
        // Act
        var snapshot = _db.Snapshot();

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.DbContext.ShouldBe(_db);
    }

    [Fact]
    public async Task SnapshotContext_Dispose_ShouldReleaseResources()
    {
        // Arrange
        var snapshot = _db.Snapshot();

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
        var snapshot = _db.Snapshot();
        var snapshotEntities = snapshot.SnapshotEntities;

        // Assert
        snapshotEntities.ShouldNotBeEmpty();
        snapshotEntities.ShouldContain(e => e.Entity == user);
    }

    [Fact]
    public void SnapshotEntities_MultipleAccess_ShouldReturnSameInstance()
    {
        // Arrange
        var snapshot = _db.Snapshot();

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
        
        var snapshot = _db.Snapshot();
        var entry = snapshot.SnapshotEntities[0];

        // Assert
        entry.Entity.ShouldBe(user);
        entry.OriginalState.ShouldBe(Microsoft.EntityFrameworkCore.EntityState.Added);
    }
}