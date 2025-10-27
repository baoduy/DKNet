namespace EfCore.Extensions.Tests;

public class UserTests(MemoryFixture fixture) : IClassFixture<MemoryFixture>
{
    #region Fields

    private readonly MyDbContext _db = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public async Task AddUserAndAddress()
    {
        var user = new User("A")
        {
            FirstName = "Duy",
            LastName = "Hoang",
            Addresses =
            {
                new Address
                {
                    OwnedEntity = new OwnedEntity { Name = "123" },
                    City = "HBD",
                    Street = "HBD"
                },
                new Address
                {
                    OwnedEntity = new OwnedEntity { Name = "123" },
                    City = "HBD",
                    Street = "HBD"
                }
            }
        };

        _db.Add(user);
        await _db.SaveChangesAsync();

        var u = await _db.Set<User>().Include(i => i.Addresses).FirstAsync(i => i.Id == user.Id);
        u.ShouldNotBeNull();
        u.Addresses.Count.ShouldBeGreaterThanOrEqualTo(2);

        u.Addresses.Remove(u.Addresses.First());
        await _db.SaveChangesAsync();

        u = await _db.Set<User>().Include(i => i.Addresses).FirstAsync(i => i.Id == user.Id);
        u.Addresses.Count.ShouldBeGreaterThanOrEqualTo(1);

        _db.ChangeTracker.AutoDetectChangesEnabled.ShouldBeTrue();
    }


    [Fact]
    public void CreatedUserIdShouldBeZero()
    {
        var user = new User("Duy")
            { FirstName = "Steven", LastName = "Smith" };
        user.Id.ShouldBe(0);
    }

    #endregion
}