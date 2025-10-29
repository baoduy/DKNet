namespace EfCore.Extensions.Tests;

public class ExtensionsTests(MemoryFixture fixture) : IClassFixture<MemoryFixture>
{
    #region Fields

    private readonly MyDbContext _db = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public void TestGetKeys()
    {
        this._db.GetPrimaryKeyProperties<User>().Single()
            .ShouldBe("Id");
    }

    [Fact]
    public void TestGetKeyValue()
    {
        var user = new User(1, "Duy") { FirstName = "Steven", LastName = "Smith" };
        this._db.GetPrimaryKeyValues(user).Single().Value.ShouldBe(1);
    }

    [Fact]
    public void TestGetKeyValueNotEntity()
    {
        var user = new { Id = 1, Name = "Duy" };
        this._db.GetPrimaryKeyValues(user).Any()
            .ShouldBeFalse();
    }

    #endregion
}