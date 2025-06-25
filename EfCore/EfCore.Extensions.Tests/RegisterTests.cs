using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Configurations;
using DKNet.EfCore.Extensions.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Extensions.Tests;

[TestClass]
public class RegisterTests : SqlServerTestBase
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

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        _db?.Dispose();
        await CleanupContainerAsync(_sql);
    }

    [TestMethod]
    public async Task TestRegisterEntitiesDefaultOptions()
    {
        //Create User with Address
        await _db.Set<User>().AddAsync(new User("Duy")
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

        await _db.SaveChangesAsync().ConfigureAwait(false);

        Assert.IsTrue(await _db.Set<User>().CountAsync().ConfigureAwait(false) >= 1);
        Assert.IsTrue(await _db.Set<Address>().CountAsync().ConfigureAwait(false) >= 1);
    }

    // [TestMethod]
    // public async Task TestAccountStatusDataSeeding()
    // {
    //     await using var db = new MyDbContext(new DbContextOptionsBuilder()
    //         .UseSqliteMemory()
    //
    //         //No Assembly provided it will scan the MyDbContext assembly.
    //         .UseAutoConfigModel()
    //         .Options);
    //     await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
    //     (await db.Set<AccountStatus>().CountAsync().ConfigureAwait(false)).ShouldBeGreaterThanOrEqualTo(2);
    // }

    [TestMethod]
    public async Task TestCreateDb()
    {
        using var sql = await StartSqlContainerAsync();
        await using var db = CreateDbContext(sql.GetConnectionString());
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);

        //Create User with Address
        await db.Set<User>().AddAsync(new User("Duy")
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

        await db.SaveChangesAsync().ConfigureAwait(false);

        var users = await db.Set<User>().ToListAsync().ConfigureAwait(false);
        var adds = await db.Set<Address>().ToListAsync().ConfigureAwait(false);

        Assert.IsTrue(users.Count == 1);
        Assert.IsTrue(adds.Count == 1);

        await CleanupContainerAsync(sql);
    }

    [TestMethod]
    //[ExpectedException(typeof(DbUpdateException))]
    public async Task TestCreateDbCustomMapper()
    {
        using var sql = await StartSqlContainerAsync();
        await using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqlServer(sql.GetConnectionString())
            //No Assembly provided it will scan the MyDbContext assembly.
            .UseAutoConfigModel(op => op.ScanFrom(typeof(MyDbContext).Assembly)
                .WithDefaultMappersType(typeof(DefaultEntityTypeConfiguration<>)))
            .Options);
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);

        //Create User with Address
        await db.Set<User>().AddAsync(new User("Duy")
        {
            FirstName = "Duy",
            LastName = "Hoang",
            Addresses =
            {
                new Address {Street = "123"}
            },
        });

        await db.SaveChangesAsync().ConfigureAwait(false);

        (await db.Set<Address>().AnyAsync().ConfigureAwait(false)).ShouldBeTrue();
        await CleanupContainerAsync(sql);
    }

    [TestMethod]
    public async Task TestCreateDbNoAssembly()
    {
        using var sql = await StartSqlContainerAsync();
        using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqlServer(sql.GetConnectionString())
            //No Assembly provided it will scan the MyDbContext assembly.
            .UseAutoConfigModel(op => op.ScanFrom()
                .WithDefaultMappersType(typeof(DefaultEntityTypeConfiguration<>)))
            .Options);
        await CleanupContainerAsync(sql);
    }

    [TestMethod]
    public async Task TestCreateDbWithAssembly()
    {
        using var sql = await StartSqlContainerAsync();
        using var db = new MyDbContext(new DbContextOptionsBuilder<MyDbContext>()
            //No Assembly provided it will scan the MyDbContext assembly.
            .UseAutoConfigModel(op => op.ScanFrom(typeof(MyDbContext).Assembly))
            .UseSqlServer(sql.GetConnectionString())
            .Options);
        await CleanupContainerAsync(sql);
    }

    [TestMethod]
    //[ExpectedException(typeof(DbUpdateException))]
    public async Task TestCreateDbValidate()
    {
        using var sql = await StartSqlContainerAsync();
        await using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqlServer(sql.GetConnectionString())
            .UseAutoConfigModel(op => op.ScanFrom(typeof(MyDbContext).Assembly)
                .WithDefaultMappersType(typeof(DefaultEntityTypeConfiguration<>)))
            .Options);
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);

        //Create User with Address
        await db.Set<User>().AddAsync(new User("Duy")
        {
            FirstName = "Duy",
            LastName = "Hoang",
            Addresses =
            {
                new Address {Street = "123"}
            },
        });

        await db.SaveChangesAsync().ConfigureAwait(false);
        await CleanupContainerAsync(sql);
    }

    [TestMethod]
    public async Task TestEnumStatus1DataSeeding()
    {
        using var sql = await StartSqlContainerAsync();
        await using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqlServer(sql.GetConnectionString())
            //No Assembly provided it will scan the MyDbContext assembly.
            .UseAutoConfigModel()
            .Options);
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
        (await db.Set<EnumTables<EnumStatus1>>().CountAsync().ConfigureAwait(false)).ShouldBe(3);
        await CleanupContainerAsync(sql);
    }

    [TestMethod]
    public async Task TestEnumStatusDataSeeding()
    {
        using var sql = await StartSqlContainerAsync();
        await using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqlServer(sql.GetConnectionString())
            //No Assembly provided it will scan the MyDbContext assembly.
            .UseAutoConfigModel()
            .Options);
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
        (await db.Set<EnumTables<EnumStatus>>().CountAsync().ConfigureAwait(false)).ShouldBe(3);

        var first = await db.Set<EnumTables<EnumStatus>>().FirstAsync().ConfigureAwait(false);
        first.Id.ShouldBeGreaterThanOrEqualTo(0);
        await CleanupContainerAsync(sql);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public async Task TestIgnoredEntityAsync()
    {
        using var sql = await StartSqlContainerAsync();
        await using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqlServer(sql.GetConnectionString())
            .UseAutoConfigModel(op =>
                op.ScanFrom(typeof(MyDbContext).Assembly)
                    .WithDefaultMappersType(typeof(DefaultEntityTypeConfiguration<>)))
            .Options);

        await db.Database.EnsureCreatedAsync();

        var list = await db.Set<IgnoredAutoMapperEntity>().ToListAsync();
        await CleanupContainerAsync(sql);
    }

    [TestMethod]
    //[Ignore]
    [ExpectedException(typeof(InvalidOperationException))]
    public async Task TestWithCustomEntityMapperBad()
    {
        using var sql = await StartSqlContainerAsync();
        using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqlServer(sql.GetConnectionString())
            .UseAutoConfigModel(op =>
                op.ScanFrom(typeof(MyDbContext).Assembly).WithDefaultMappersType(typeof(Entity<>)))
            .Options);

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        await CleanupContainerAsync(sql);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task TestWithCustomEntityMapperNullFilterBad()
    {
        using var sql = await StartSqlContainerAsync();
        using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqlServer(sql.GetConnectionString())
            .UseAutoConfigModel(op =>
                op.ScanFrom(typeof(MyDbContext).Assembly).WithFilter(null))
            .Options);
        await CleanupContainerAsync(sql);
    }
}