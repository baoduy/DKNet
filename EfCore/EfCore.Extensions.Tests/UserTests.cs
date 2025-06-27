namespace EfCore.Extensions.Tests;

[TestClass]
public class UserTests : SqlServerTestBase
{
    private static MsSqlContainer _sql;
    private static MyDbContext _db;

    [ClassInitialize]
    public static async Task ClassSetup(TestContext _)
    {
        _sql = await StartSqlContainerAsync();
        _db = CreateDbContext(_sql.GetConnectionString());
        await _db.Database.EnsureCreatedAsync();
    }

    [TestMethod]
    public void CreatedUserIdShouldBeZero()
    {
        var user = new User("Duy") { FirstName = "Steven", LastName = "Smith" };
        user.Id.ShouldBe(0);
    }

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

        _db.Add(user);
        _db.SaveChanges();

        var u = _db.Set<User>().Include(i => i.Addresses).First();
        u.ShouldNotBeNull();
        u.Addresses.Count.ShouldBeGreaterThanOrEqualTo(1);

        u.Addresses.Remove(u.Addresses.First());
        _db.SaveChanges();

        u = _db.Set<User>().First();
        u.Addresses.Count.ShouldBeGreaterThanOrEqualTo(1);

        _db.ChangeTracker.AutoDetectChangesEnabled.ShouldBeTrue();
    }
}