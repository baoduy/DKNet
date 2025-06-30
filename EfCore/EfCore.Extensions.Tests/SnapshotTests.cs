namespace EfCore.Extensions.Tests;

[TestClass]
public class SnapshotTests : SqlServerTestBase
{
    private static MyDbContext _db;

    [ClassInitialize]
    public static async Task ClassSetup(TestContext _)
    {
        await StartSqlContainerAsync();
        _db = CreateDbContext("EventDb");
        await _db.Database.EnsureCreatedAsync();
    }

    [TestMethod]
    public void Snapshot_ShouldCreateSnapshotContext()
    {
        // Act
        var snapshot = _db.Snapshot();

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.DbContext.ShouldBe(_db);
    }

    [TestMethod]
    public async Task SnapshotContext_Dispose_ShouldReleaseResources()
    {
        // Arrange
        var snapshot = _db.Snapshot();

        // Act
        await snapshot.DisposeAsync();

        // Assert
        Should.Throw<ObjectDisposedException>(() => snapshot.DbContext);
    }

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public void SnapshotEntityEntry_ShouldCaptureEntityState()
    {
        // Arrange
        var user = new User("Test Creator") { FirstName = "Test", LastName = "User" };
        _db.Set<User>().Add(user);
        
        var snapshot = _db.Snapshot();
        var entry = snapshot.SnapshotEntities.First();

        // Assert
        entry.Entity.ShouldBe(user);
        entry.OriginalState.ShouldBe(Microsoft.EntityFrameworkCore.EntityState.Added);
    }
}