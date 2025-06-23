

namespace EfCore.Extensions.Tests;

[TestClass]
public class DefaultKeyTests
{
    [TestMethod]
    public void DefaultKey()
    {
        new User("Duy") { FirstName = "Steven", LastName = "Smith" }.Id.ShouldBe(0);
        new Address().Id.ShouldBe(0);
    }
}