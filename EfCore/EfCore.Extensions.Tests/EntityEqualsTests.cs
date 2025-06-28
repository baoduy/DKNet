namespace EfCore.Extensions.Tests;

[TestClass]
public class EntityEqualsTests
{
    // [TestMethod]
    // public void TestGetHashCode()
    // {
    //     var user1 = new User(0, "Hello") { FirstName = "Steven", LastName = "Smith" };
    //     var user2 = new User(-10, "BBB"){ FirstName = "Steven", LastName = "Smith" };

    //     //user1.GetHashCode().ShouldNotBe(user2.GetHashCode());

    //     var user3 = new User(1, "Hello"){ FirstName = "Steven", LastName = "Smith" };
    //     var user4 = new User(1, "Hello"){ FirstName = "Steven", LastName = "Smith" };

    //     //user3.GetHashCode().ShouldBe(user4.GetHashCode());
    // }

    [TestMethod]
    public void TestReferenceEquals()
    {
        var user1 = new User(1, "Hello")
        {
            Account = new Account { UserName = "Steven", Password = "Pass@word1" }, FirstName = "Steven",
            LastName = "Smith"
        };
        var user2 = new User(0, "BBB")
        {
            Account = new Account { UserName = "Steven", Password = "Pass@word1" }, FirstName = "Steven",
            LastName = "Smith"
        };

        user1.Equals(user2).ShouldBeFalse();
        user1.Equals(user1).ShouldBeTrue();
        user2.Equals(user2).ShouldBeTrue();
    }

    //[TestMethod]
    //public void TestEquals()
    //{
    //    var user1 = new User(1, "Hello");
    //    var user2 = new User(1, "Hello");
    //    var user3 = new User(2,"Hoang");
    //    var user4 = new User(0,"AAA");
    //    var user5 = new User(0,"BBB");

    //    user1.Equals(user2).ShouldBeTrue();
    //    user1.Equals(user3).ShouldBeFalse();
    //    user3.Equals(null).ShouldBeFalse();
    //    user3.Equals(new Address()).ShouldBeFalse();

    //    user5.Equals(user4).ShouldBeFalse("Two default Key entity should not be the same");
    //}

    //[TestMethod]
    //public void TestEqualsOperator()
    //{
    //    var user1 = new User(1, "Hello");
    //    var user2 = new User(1, "Hello");
    //    var user3 = new User(2,"Hoang");

    //    (user1 == user2).ShouldBeTrue();
    //    (user1 == user3).ShouldBeFalse();
    //    (user3 == null).ShouldBeFalse();
    //    (user3 == new Address()).ShouldBeFalse();
    //}

    [TestMethod]
    public void CanRemoveWhereItemFromHashSet()
    {
        var set = new HashSet<User>
        {
            new(1, "Hoang")
                { FirstName = "Steven", LastName = "Smith" },
            new(2, "Duy")
                { FirstName = "Steven", LastName = "Smith" },
        };

        set.RemoveWhere(u => u.Id == 1);

        set.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void CanRemoveItemFromHashSet()
    {
        var set = new HashSet<User>
        {
            new(1, "Hoang")
                { FirstName = "Steven", LastName = "Smith" },
            new(2, "Duy")
                { FirstName = "Steven", LastName = "Smith" },
        };

        set.Remove(set.First());

        set.Count.ShouldBeGreaterThanOrEqualTo(1);
    }
}