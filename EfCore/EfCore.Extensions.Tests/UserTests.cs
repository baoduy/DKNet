namespace EfCore.Extensions.Tests;

[TestClass]
public class UserTests
{

    [TestMethod]
    public void AddUserAndAddress()
    {
        var user = new User("A")
        {
            FirstName = "Duy",
            LastName = "Hoang",
            Addresses =
            {
                new Address
                {
                    OwnedEntity = new OwnedEntity
                    {
                        Name = "A"
                    },
                    Street = "123"
                },
                new Address
                {
                    OwnedEntity = new OwnedEntity
                    {
                        Name = "B"
                    },
                    Street = "124"
                }
            },
        };

        UnitTestSetup.Db.Add(user);
        UnitTestSetup.Db.SaveChanges();

        var u = UnitTestSetup.Db.Set<User>().Include(i => i.Addresses).First();
        u.ShouldNotBeNull();
        u.Addresses.Count.ShouldBeGreaterThanOrEqualTo(2);

        u.Addresses.Remove(u.Addresses.First());
        UnitTestSetup.Db.SaveChanges();

        u = UnitTestSetup.Db.Set<User>().First();
        u.Addresses.Count.ShouldBeGreaterThanOrEqualTo(1);

        UnitTestSetup.Db.ChangeTracker.AutoDetectChangesEnabled.ShouldBeTrue();
    }

    [TestMethod]
    public void CreatedUserIdShouldBeZero()
    {
        var user = new User("Duy") { FirstName = "Steven", LastName = "Smith" };
        user.Id.ShouldBe(0);
    }
    
}