namespace EfCore.Extensions.Tests;

[TestClass]
public class DefaultKeyTests
{
    [TestMethod]
    public void DefaultKey()
    {
        new User("Duy")
            { FirstName = "Steven", LastName = "Smith" }.Id.ShouldBe(0);
        new Address
        {
            OwnedEntity = new OwnedEntity{Name = "123"},
            City = "HBD",
            Street = "HBD"
        }.Id.ShouldBe(0);
    }
}