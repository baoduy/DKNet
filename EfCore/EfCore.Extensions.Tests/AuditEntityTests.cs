namespace EfCore.Extensions.Tests;

[TestClass]
public class AuditEntityTests
{

    [TestMethod]
    public void TestCreatingEntity()
    {
        var user = new User("Duy") { FirstName = "Steven", LastName = "Smith" };
        user.UpdatedByUser("Hoang");

        //TODO allow updatedBy the same with createdBy
        //user.UpdatedBy.ShouldBeNullOrEmpty();
        //user.UpdatedOn.ShouldBeNull();

        user.Id.ShouldBe(0);
    }

    [TestMethod]
    public async Task TestUpdatingEntityAsync()
    {
        await UnitTestSetup.Db.SeedData().ConfigureAwait(false);
        UnitTestSetup.Db.ChangeTracker.Clear();

        var user = await UnitTestSetup.Db.Set<User>().FirstAsync();
        user.ShouldNotBeNull();

        user.UpdatedByUser("Hoang");

        user.UpdatedBy.ShouldBe("Hoang");
        user.UpdatedOn.ShouldNotBeNull();
        user.Id.ShouldBe(1);
    }
}