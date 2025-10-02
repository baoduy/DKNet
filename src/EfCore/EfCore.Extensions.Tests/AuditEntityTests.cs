namespace EfCore.Extensions.Tests;

public class AuditEntityTests(MemoryFixture fixture) : IClassFixture<MemoryFixture>
{
    private readonly MyDbContext _db = fixture.Db!;

    [Fact]
    public void TestCreatingEntity()
    {
        var user = new User("Duy") { FirstName = "Steven", LastName = "Smith" };
        user.UpdatedByUser("Hoang");
        user.Id.ShouldBe(0);
    }

    [Fact]
    public async Task TestUpdatingEntityAsync()
    {
        await _db.Set<User>()
            .AddRangeAsync(new User("StevenHoang")
            {
                FirstName = "Steven",
                LastName = "Hoang"
            }, new User("DuyHoang")
            {
                FirstName = "Duy",
                LastName = "Hoang"
            });
        await _db.SaveChangesAsync();

        _db.ChangeTracker.Clear();

        var user = await _db.Set<User>().FirstAsync();
        user.ShouldNotBeNull();
        user.UpdatedByUser("Hoang");

        user.UpdatedBy.ShouldBe("Hoang");
        user.UpdatedOn.ShouldNotBeNull();
        user.Id.ShouldBe(1);
    }
}