namespace EfCore.Extensions.Tests;

public class DefaultKeyTests
{
    #region Methods

    [Fact]
    public void DefaultKey()
    {
        new User("Duy")
            { FirstName = "Steven", LastName = "Smith" }.Id.ShouldBe(0);
        new Address
        {
            OwnedEntity = new OwnedEntity { Name = "123" },
            City = "HBD",
            Street = "HBD"
        }.Id.ShouldBe(0);
    }

    #endregion
}