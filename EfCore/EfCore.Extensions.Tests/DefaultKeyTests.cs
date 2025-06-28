

namespace EfCore.Extensions.Tests;

[TestClass]
public class DefaultKeyTests
{
    [TestMethod]
    public void DefaultKey()
    {
        new User("Duy", new Account{UserName = "Steven",Password = "Pass@word1"}) { FirstName = "Steven", LastName = "Smith" }.Id.ShouldBe(0);
        new Address(new OwnedEntity("123","123","Steven","AAA","qqq")).Id.ShouldBe(0);
    }
}