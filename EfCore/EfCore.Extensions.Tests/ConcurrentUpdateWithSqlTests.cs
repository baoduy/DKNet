namespace EfCore.Extensions.Tests;

/// <summary>
/// Concurrent update needs to be tested with real SQL Server.
/// </summary>
[TestClass]
public class ConcurrentUpdateWithSqlTests
{
    private static MsSqlContainer _sql = null!;
    private static MyDbContext _db= null!;

    [ClassInitialize]
    public static async Task Setup(TestContext context)
    {
        _sql = new MsSqlBuilder().WithPassword("a1ckZmGjwV8VqNdBUexV").Build();
        await _sql.StartAsync(context.CancellationTokenSource.Token);
        await Task.Delay(TimeSpan.FromSeconds(5), context.CancellationTokenSource.Token);

        var options = new DbContextOptionsBuilder()
            .LogTo(Console.WriteLine, (eventId, logLevel) => logLevel >= LogLevel.Information
                                                             || eventId == RelationalEventId.CommandExecuting)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .UseSqlServer(_sql.GetConnectionString())
            .UseAutoConfigModel(op => op.ScanFrom(typeof(MyDbContext).Assembly))
            .Options;

        _db = new MyDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    [TestMethod]
    public async Task ConcurrencyWithRepositoryTest()
    {
        var writeRepo = new WriteRepository<User>(_db);
        var readRepo = new ReadRepository<User>(_db);
        //1. Create a new User.
        var user = new User("A")
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
                },
                new Address
                {
                    OwnedEntity = new OwnedEntity{Name = "123"},
                    City = "HBD",
                    Street = "HBD"
                }
            },
        };

        writeRepo.Add(user);
        await writeRepo.SaveChangesAsync();

        var createdVersion = (byte[])user.RowVersion!.Clone();

        //2. Update user with created version. It should allow to update.
        // Change the person's name in the database to simulate a concurrency conflict.
        user.FirstName = "Duy3";
        user.SetUpdatedBy("System");
        await writeRepo.SaveChangesAsync();

        //3. Update user with created version again. It should NOT allow to update.
        user = await readRepo.FindAsync(user.Id);
        user!.FirstName = "Duy3";
        user.SetRowVersion(createdVersion);

        //The DbUpdateConcurrencyException will be throw here
        Func<Task> fun = async () => { writeRepo.Update(user); await writeRepo.SaveChangesAsync(); };
        await fun.ShouldThrowAsync<DbUpdateConcurrencyException>();
    }
}