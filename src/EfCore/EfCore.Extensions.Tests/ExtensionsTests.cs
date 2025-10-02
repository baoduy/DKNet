namespace EfCore.Extensions.Tests;

public class ExtensionsTests(MemoryFixture fixture) : IClassFixture<MemoryFixture>
{
    private readonly MyDbContext _db = fixture.Db!;


    [Fact]
    public void TestGetKeys()
    {
        _db.GetPrimaryKeyProperties<User>().Single()
            .ShouldBe("Id");
    }

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