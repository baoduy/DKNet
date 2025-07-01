namespace EfCore.Extensions.Tests;


public class AuditEntityTests : SqlServerTestBase
{

    private static MyDbContext _db = null!;

    
    public static async Task ClassSetup()
    {
        await StartSqlContainerAsync();
        _db = CreateDbContext("AuditDb");
        await _db.Database.EnsureCreatedAsync(context.CancellationTokenSource.Token);
    }

    
    public async Task TestInitialize()
    {
        await EnsureSqlStartedAsync();
    }

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
        _db.Set<User>().AddRange(new User("StevenHoang")
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