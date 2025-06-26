namespace EfCore.Extensions.Tests;

#pragma warning disable CA2012 // Use ValueTasks correctly
[TestClass]
public class WithSqlDbTests : SqlServerTestBase
{
    private static MsSqlContainer _sql;
    private static MyDbContext _db;

    [ClassInitialize]
    public async Task Setup(TestContext _)
    {
        _sql = await StartSqlContainerAsync();
        await _sql.StartAsync();
        await Task.Delay(TimeSpan.FromSeconds(20));

        _db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqlServer(_sql.GetConnectionString())
            .UseAutoConfigModel(op => op.ScanFrom(typeof(MyDbContext).Assembly))
            .UseAutoDataSeeding()
            .Options);

        await _db.Database.EnsureCreatedAsync();
    }

    [TestInitialize]
    public Task TestSetup()
    {
        return EnsureSqlStartedAsync();
    }

    [TestMethod]
    public async Task SequenceValueTestAsync()
    {
        var val1 = await _db.NextSeqValue<SequencesTest, short>(SequencesTest.Invoice);
        val1!.Value.ShouldBeGreaterThan((short)0);

        var val2 = await _db.NextSeqValue<SequencesTest, int>(SequencesTest.Order);
        val2!.Value.ShouldBeGreaterThan(0);
    }

    [TestMethod]
    public async Task SequenceValueWithFormatTestAsync()
    {
        var val1 = await _db.NextSeqValueWithFormat(SequencesTest.Invoice);
        val1.ShouldContain(string.Format(CultureInfo.CurrentCulture, "T{0:yyMMdd}0000", DateTime.Now));
    }

    [TestMethod]
    public async Task TestDataSeeding()
    {
        var account = await _db.Set<AccountStatus>().CountAsync();
        account.ShouldBeGreaterThanOrEqualTo(2);
    }

    [TestMethod]
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
                    Street = "12"
                }
            },
        });

        var count = await _db.SaveChangesAsync();
        Assert.IsTrue(count >= 1);

        var users = await _db.Set<User>().ToListAsync();

        Assert.IsTrue(users.Count >= 1);
        Assert.IsTrue(users.All(u => u.RowVersion != null));
    }

    [TestMethod]
    public async Task TestDeleteWithSqlDbAsync()
    {
        await TestCreateWithSqlDbAsync();

        var user = await _db.Set<User>().Include(u => u.Addresses).FirstAsync();

        _db.RemoveRange(user.Addresses);
        _db.Remove(user);

        await _db.SaveChangesAsync();

        var count = await _db.Set<User>().CountAsync(u => u.Id == user.Id);

        Assert.IsTrue(count == 0);
    }

    [TestMethod]
    public async Task TestUpdateWithSqlDbAsync()
    {
        await TestCreateWithSqlDbAsync();

        var user = await _db.Set<User>().Include(u => u.Addresses).FirstAsync();

        user.FirstName = "Steven";
        user.Addresses.Last().Street = "Steven Street";

        await _db.SaveChangesAsync();

        user = await _db.Set<User>().FirstAsync();

        Assert.IsTrue(string.Equals(user.FirstName, "Steven", StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(string.Equals(user.Addresses.Last().Street, "Steven Street", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
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