namespace EfCore.Extensions.Tests;

/// <summary>
/// Concurrent update needs to be tested with real SQL Server.
/// </summary>
[TestClass]
public class ConcurrentUpdateWithSqlTests
{
    private static MsSqlContainer _sql;
    private static MyDbContext _db;

    [ClassInitialize]
    public async Task Setup(TestContext _)
    {
        _sql = new MsSqlBuilder().WithPassword("a1ckZmGjwV8VqNdBUexV").Build();
        await _sql.StartAsync();
        await Task.Delay(TimeSpan.FromSeconds(5));

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
    public async Task ConcurrencyWithDbContextTest()
    {
        //1. Create a new User.
        var user = new User("A", new Account{UserName = "Steven",Password = "Pass@word1"})
        {
            FirstName = "Duy",
            LastName = "Hoang",
            Addresses =
            {
                new Address(new OwnedEntity("123","123","Steven","AAA","qqq"))
                {
                    Street = "123"
                },
                new Address(new OwnedEntity("123","123","Steven","AAA","qqq"))
                {
                    Street = "124"
                }
            },
        };

        await _db.AddAsync(user);
        await _db.SaveChangesAsync();

        user = await _db.Set<User>().FirstAsync(u => u.Id == user.Id);
        var createdVersion = (byte[])user.RowVersion!.Clone();

        //2. Update user with created version. It should allow to update.
        // Change the person's name in the database to simulate a concurrency conflict.
        await _db.Database.ExecuteSqlRawAsync(
            $"UPDATE [User] SET [FirstName] = 'Duy2' WHERE Id = {user.Id}");

        //3. Update user with created version again. It should NOT allow to update.
        user.FirstName = "Duy3";
        user.SetUpdatedBy("System");

        //The DbUpdateConcurrencyException will be throwing here
        Func<Task> fun = () => _db.SaveChangesAsync();
        await fun.ShouldThrowAsync<DbUpdateConcurrencyException>();

        //4. Check the RowVersion in Db
        await _db.Entry(user).ReloadAsync();
        var updatedVersion = (byte[])user.RowVersion.Clone();

        updatedVersion.ShouldNotBe(createdVersion);

        //5. Update user again with latest row version.
        //NOTE this will overwrite the changes in Database. Check the latest data before execute.
        await _db.SaveChangesAsync();
    }

    [TestMethod]
    public async Task ConcurrencyWithRepositoryTest()
    {
        var writeRepo = new WriteRepository<User>(_db);
        var readRepo = new ReadRepository<User>(_db);
        //1. Create a new User.
        var user = new User("A",new Account{UserName = "Steven",Password = "Pass@word1"})
        {
            FirstName = "Duy",
            LastName = "Hoang",
            Addresses =
            {
                new Address(new OwnedEntity("123","123","Steven","AAA","qqq"))
                {

                    Street = "123"
                },
                new Address(new OwnedEntity("123","123","Steven","AAA","qqq"))
                {

                    Street = "124"
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
        user.FirstName = "Duy3";
        user.SetRowVersion(createdVersion);

        //The DbUpdateConcurrencyException will be throw here
        Func<Task> fun = async () => { writeRepo.Update(user); await writeRepo.SaveChangesAsync(); };
        await fun.ShouldThrowAsync<DbUpdateConcurrencyException>();
    }
}