namespace EfCore.Extensions.Tests;

#pragma warning disable CA2012 // Use ValueTasks correctly
[TestClass]
public class WithSqlDbTests
{
    private static MsSqlContainer _sql;
    private static MyDbContext _db;

    [ClassCleanup]
    public static void CleanUp()
    {
        _sql.StopAsync().GetAwaiter().GetResult();
        _sql.DisposeAsync().GetAwaiter().GetResult();
        _db?.Dispose();
    }

    [ClassInitialize]
    public static void Setup(TestContext _)
    {
        _sql = new MsSqlBuilder().WithPassword("a1ckZmGjwV8VqNdBUexV").WithAutoRemove(true).Build();
        _sql.StartAsync().GetAwaiter().GetResult();
        Task.Delay(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();

        _db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqlServer(_sql.GetConnectionString())
            .UseAutoConfigModel(op => op.ScanFrom(typeof(MyDbContext).Assembly))
            .UseAutoDataSeeding()
            .Options);

        _db.Database.EnsureCreated();
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

        var count = await _db.SaveChangesAsync().ConfigureAwait(false);
        Assert.IsTrue(count >= 1);

        var users = await _db.Set<User>().ToListAsync().ConfigureAwait(false);

        Assert.IsTrue(users.Count >= 1);
        Assert.IsTrue(users.All(u => u.RowVersion != null));
    }

    [TestMethod]
    public async Task TestDeleteWithSqlDbAsync()
    {
        await TestCreateWithSqlDbAsync().ConfigureAwait(false);

        var user = await _db.Set<User>().Include(u => u.Addresses).FirstAsync().ConfigureAwait(false);

        _db.RemoveRange(user.Addresses);
        _db.Remove(user);

        await _db.SaveChangesAsync().ConfigureAwait(false);

        var count = await _db.Set<User>().CountAsync(u => u.Id == user.Id).ConfigureAwait(false);

        Assert.IsTrue(count == 0);
    }

    [TestMethod]
    public async Task TestUpdateWithSqlDbAsync()
    {
        await TestCreateWithSqlDbAsync().ConfigureAwait(false);

        var user = await _db.Set<User>().Include(u => u.Addresses).FirstAsync().ConfigureAwait(false);

        user.FirstName = "Steven";
        user.Addresses.Last().Street = "Steven Street";

        await _db.SaveChangesAsync().ConfigureAwait(false);

        user = await _db.Set<User>().FirstAsync().ConfigureAwait(false);

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
        await _db.SaveChangesAsync().ConfigureAwait(false);

        // Create new contexts with same configuration
#pragma warning disable EF1001 // Internal EF Core API usage.
        var dbOptions = _db.GetService<IDbContextServices>().ContextOptions;

        using var db1 = new MyDbContext(dbOptions);
        using var db2 = new MyDbContext(dbOptions);

        var user1 = await db1.Set<User>().FindAsync(user.Id).ConfigureAwait(false);
        var user2 = await db2.Set<User>().FindAsync(user.Id).ConfigureAwait(false);

        // Act - First update
        user1!.FirstName = "Updated1";
        await db1.SaveChangesAsync().ConfigureAwait(false);

        // Second update should conflict
        user2!.FirstName = "Updated2";
        Func<Task> act = async () => await db2.SaveChangesAsync().ConfigureAwait(false);

        // Assert
        await act.ShouldThrowAsync<DbUpdateConcurrencyException>();
    }
}