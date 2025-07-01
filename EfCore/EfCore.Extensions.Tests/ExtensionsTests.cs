namespace EfCore.Extensions.Tests;


public class ExtensionsTests : SqlServerTestBase
{
    private static MyDbContext _db;

    
    public static async Task ClassSetup()
    {
        await StartSqlContainerAsync();
        _db = CreateDbContext("TestDb");
        await _db.Database.EnsureCreatedAsync();
    }

    [Fact]
    public void TestGetKeys()
    {
        _db.GetPrimaryKeyProperties<User>().Single()
            .ShouldBe("Id");
    }

    // [Fact]
    // public void Test_GetKeys_NotEntity()
    // {
    //     _db.GetKeys<UserAccountStartWithDSpec>().Any()
    //         .ShouldBeFalse();
    // }

    [Fact]
    public void TestGetKeyValue()
    {
        var user = new User(1, "Duy") { FirstName = "Steven", LastName = "Smith" };
        _db.GetPrimaryKeyValues(user).Single()
            .ShouldBe(1);
    }

    [Fact]
    public void TestGetKeyValueNotEntity()
    {
        var user = new { Id = 1, Name = "Duy" };
        _db.GetPrimaryKeyValues(user).Any()
            .ShouldBeFalse();
    }
}