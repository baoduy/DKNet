namespace EfCore.Extensions.Tests;

[TestClass]
public class ExtensionsTests : SqlServerTestBase
{
    private static MsSqlContainer _sql;
    private static MyDbContext _db;

    [ClassInitialize]
    public async Task ClassSetup(TestContext _)
    {
        _sql = await StartSqlContainerAsync();
        _db = CreateDbContext(_sql.GetConnectionString());
        await _db.Database.EnsureCreatedAsync();
    }

    [TestMethod]
    public void TestGetKeys()
    {
        _db.GetPrimaryKeyProperties<User>().Single()
            .ShouldBe("Id");
    }

    // [TestMethod]
    // public void Test_GetKeys_NotEntity()
    // {
    //     _db.GetKeys<UserAccountStartWithDSpec>().Any()
    //         .ShouldBeFalse();
    // }

    [TestMethod]
    public void TestGetKeyValue()
    {
        var user = new User(1, "Duy") { FirstName = "Steven", LastName = "Smith" };
        _db.GetPrimaryKeyValues(user).Single()
            .ShouldBe(1);
    }

    [TestMethod]
    public void TestGetKeyValueNotEntity()
    {
        var user = new { Id = 1, Name = "Duy" };
        _db.GetPrimaryKeyValues(user).Any()
            .ShouldBeFalse();
    }
}