namespace EfCore.Extensions.Tests;

[TestClass]
public class AuditEntityTests : SqlServerTestBase
{
    private MsSqlContainer _sql;
    private MyDbContext _db;

    [ClassInitialize]
    public async Task ClassSetup(TestContext _)
    {
        _sql = await StartSqlContainerAsync();
        _db = CreateDbContext(_sql.GetConnectionString());
        await _db.Database.EnsureCreatedAsync();
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        await EnsureSqlStartedAsync();
    }

    [TestMethod]
    public void TestCreatingEntity()
    {
        var user = new User("Duy") { FirstName = "Steven", LastName = "Smith" };
        user.UpdatedByUser("Hoang");
        user.Id.ShouldBe(0);
    }

    [TestMethod]
    public async Task TestUpdatingEntityAsync()
    {
        _db.Set<User>().AddRange(new User("StevenHoang")
        {
            FirstName = "Steven",
            LastName = "Hoang"
        }, new User("DuyHoang")
        {
            FirstName = "Duy",
            LastName = "Hoang"
        });
        await _db.SaveChangesAsync();

        _db.ChangeTracker.Clear();

        var user = await _db.Set<User>().FirstAsync();
        user.ShouldNotBeNull();
        user.UpdatedByUser("Hoang");

        user.UpdatedBy.ShouldBe("Hoang");
        user.UpdatedOn.ShouldNotBeNull();
        user.Id.ShouldBe(1);
    }
}