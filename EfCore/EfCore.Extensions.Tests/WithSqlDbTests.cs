namespace EfCore.Extensions.Tests;

#pragma warning disable CA2012 // Use ValueTasks correctly

public class WithSqlDbTests : SqlServerTestBase
{
    private static MyDbContext _db = null!;

    
    public static async Task Setup()
    {
        await StartSqlContainerAsync();

        _db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqlServer(GetConnectionString("WithSqlDb"))
            .UseAutoConfigModel(op => op.ScanFrom(typeof(MyDbContext).Assembly))
            .UseAutoDataSeeding()
            .Options);

        await _db.Database.EnsureCreatedAsync();
    }

    
    public Task TestSetup() => EnsureSqlStartedAsync();

    [Fact]
    public async Task SequenceValueTestAsync()
    {
        var val1 = await _db.NextSeqValue<SequencesTest, short>(SequencesTest.Invoice);
        val1!.Value.ShouldBeGreaterThan((short)0);

        var val2 = await _db.NextSeqValue<SequencesTest, int>(SequencesTest.Order);
        val2!.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SequenceValueWithFormatTestAsync()
    {
        var val1 = await _db.NextSeqValueWithFormat(SequencesTest.Invoice);
        val1.ShouldContain(string.Format(CultureInfo.CurrentCulture, "T{0:yyMMdd}0000", DateTime.Now));
    }

    [Fact]
    public async Task TestDataSeeding()
    {
        var account = await _db.Set<AccountStatus>().CountAsync();
        account.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task TestCreateWithSqlDbAsync()
    {
        _db.ChangeTracker.Clear();
        //Create User with Address
        _db.Set<User>().Add(new User("Duy")
        {
            FirstName = "Duy",
            LastName = "Hoang",
            Addresses =
            {
                new Address
                {
                    OwnedEntity = new OwnedEntity{Name = "123"},
                    City = "HBD",
                    Street = "HBD"
                }
            },
        });

        var count = await _db.SaveChangesAsync();
        (count >= 1).ShouldBeTrue();

        var users = await _db.Set<User>().ToListAsync();

        (users.Count >= 1).ShouldBeTrue();
        users.All(u => u.RowVersion != null).ShouldBeTrue();
    }

    [Fact]
    public async Task TestDeleteWithSqlDbAsync()
    {
        await TestCreateWithSqlDbAsync();

        var user = await _db.Set<User>().Include(u => u.Addresses).LastAsync();

        _db.RemoveRange(user.Addresses);
        _db.Remove(user);

        await _db.SaveChangesAsync();

        var count = await _db.Set<User>().CountAsync(u => u.Id == user.Id);

        (count == 0).ShouldBeTrue();
    }

    [Fact]
    public async Task TestUpdateWithSqlDbAsync()
    {
        await TestCreateWithSqlDbAsync();

        var user = await _db.Set<User>().Include(u => u.Addresses).OrderByDescending(u=>u.CreatedOn).FirstAsync();

        user.FirstName = "Steven";
        user.Addresses.Last().Street = "Steven Street";

        await _db.SaveChangesAsync();

        user = await _db.Set<User>().Include(i => i.Addresses).OrderByDescending(u=>u.CreatedOn).FirstAsync();

        string.Equals(user.FirstName, "Steven", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();

        string.Equals(user.Addresses.Last().Street, "Steven Street", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public async Task TestConcurrentUpdateThrowsExceptionAsync()
    {
        // Arrange
        var user = new User("ConcurrencyTest")
        {
            FirstName = "Test",
            LastName = "User",
        };
        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();

        // Create new contexts with same configuration
#pragma warning disable EF1001 // Internal EF Core API usage.
        var dbOptions = _db.GetService<IDbContextServices>().ContextOptions;

        await using var db1 = new MyDbContext(dbOptions);
        await using var db2 = new MyDbContext(dbOptions);

        var user1 = await db1.Set<User>().FindAsync(user.Id);
        var user2 = await db2.Set<User>().FindAsync(user.Id);

        // Act - First update
        user1!.FirstName = "Updated1";
        await db1.SaveChangesAsync();

        // Second update should conflict
        user2!.FirstName = "Updated2";
        Func<Task> act = async () => await db2.SaveChangesAsync();

        // Assert
        await act.ShouldThrowAsync<DbUpdateConcurrencyException>();
    }
}