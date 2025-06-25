namespace EfCore.Extensions.Tests;

[TestClass]
public class AuditEntityTests : SqlServerTestBase
{
    private static MsSqlContainer _sql;
    private static MyDbContext _db;

    [ClassInitialize]
    public static async Task ClassSetup(TestContext _)
    {
        _sql = await StartSqlContainerAsync();
        _db = CreateDbContext(_sql.GetConnectionString());
        await _db.Database.EnsureCreatedAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        _db?.Dispose();
        await CleanupContainerAsync(_sql);
    }

    [TestMethod]
    public void TestCreatingEntity()
    {
        var user = new User("Duy") { FirstName = "Steven", LastName = "Smith" };
        user.UpdatedByUser("Hoang");

        //TODO allow updatedBy the same with createdBy
        //user.UpdatedBy.ShouldBeNullOrEmpty();
        //user.UpdatedOn.ShouldBeNull();

        user.Id.ShouldBe(0);
    }

    [TestMethod]
    public async Task TestUpdatingEntityAsync()
    {
        await _db.SeedData().ConfigureAwait(false);
        _db.ChangeTracker.Clear();

        var user = await _db.Set<User>().FirstAsync();
        user.ShouldNotBeNull();

        user.UpdatedByUser("Hoang");

        user.UpdatedBy.ShouldBe("Hoang");
        user.UpdatedOn.ShouldNotBeNull();
        user.Id.ShouldBe(1);
    }
}