namespace EfCore.Extensions.Tests;

[TestClass]
public class ExtensionsTests
{
    [TestMethod]
    public void TestGetKeys()
    {
        UnitTestSetup.Db.GetPrimaryKeyProperties<User>().Single()
            .ShouldBe("Id");
    }

    // [TestMethod]
    // public void Test_GetKeys_NotEntity()
    // {
    //     UnitTestSetup.Db.GetKeys<UserAccountStartWithDSpec>().Any()
    //         .ShouldBeFalse();
    // }

    [TestMethod]
    public void TestGetKeyValue()
    {
        var user = new User(1, "Duy") { FirstName = "Steven", LastName = "Smith" };
        UnitTestSetup.Db.GetPrimaryKeyValues(user).Single()
            .ShouldBe(1);
    }

    [TestMethod]
    public void TestGetKeyValueNotEntity()
    {
        var user = new { Id = 1, Name = "Duy" };
        UnitTestSetup.Db.GetPrimaryKeyValues(user).Any()
            .ShouldBeFalse();
    }
}