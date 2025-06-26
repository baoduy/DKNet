using System.Diagnostics;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Internal;

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
        Trace.TraceInformation($"Sql Connection String: {_sql.GetConnectionString()}");
        Console.WriteLine($"Sql Connection String: {_sql.GetConnectionString()}");

        _db = CreateDbContext(_sql.GetConnectionString());
        await _db.Database.EnsureCreatedAsync();
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

        await _db.SaveChangesAsync();

        Assert.IsTrue(await _db.Set<User>().CountAsync() >= 1);
        Assert.IsTrue(await _db.Set<Address>().CountAsync() >= 1);
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
    //     await db.Database.EnsureCreatedAsync();
    //     (await db.Set<AccountStatus>().CountAsync()).ShouldBeGreaterThanOrEqualTo(2);
    // }

    [TestMethod]
    public async Task TestCreateDb()
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

        await _db.SaveChangesAsync();

        var users = await _db.Set<User>().ToListAsync();
        var adds = await _db.Set<Address>().ToListAsync();

        Assert.IsTrue(users.Count >= 1);
        Assert.IsTrue(adds.Count >= 1);
    }

    [TestMethod]
    public async Task TestCreateDbCustomMapper()
    {
        //Create User with Address
        await _db.Set<User>().AddAsync(new User("Duy")
        {
            FirstName = "Duy",
            LastName = "Hoang",
            Addresses =
            {
                new Address { Street = "123" }
            },
        });

        await _db.SaveChangesAsync();

        (await _db.Set<Address>().AnyAsync()).ShouldBeTrue();
    }


    [TestMethod]
    public async Task TestCreateDbValidate()
    {
        //Create User with Address
        await _db.Set<User>().AddAsync(new User("Duy")
        {
            FirstName = "Duy",
            LastName = "Hoang",
            Addresses =
            {
                new Address { Street = "123" }
            },
        });

        await _db.SaveChangesAsync();
    }

    [TestMethod]
    public async Task TestEnumStatus1DataSeeding()
    {
        (await _db.Set<EnumTables<EnumStatus1>>().CountAsync()).ShouldBe(3);
    }

    [TestMethod]
    public async Task TestEnumStatusDataSeeding()
    {
        (await _db.Set<EnumTables<EnumStatus>>().CountAsync()).ShouldBe(3);
        var first = await _db.Set<EnumTables<EnumStatus>>().FirstAsync();
        first.Id.ShouldBeGreaterThanOrEqualTo(0);
    }

    [TestMethod]
    public async Task TestIgnoredEntityAsync()
    {
        var action =()=>  _db.Set<IgnoredAutoMapperEntity>().ToListAsync();
        await action.ShouldThrowAsync<InvalidOperationException>();
    }

    [TestMethod]
    public async Task TestWithCustomEntityMapperBad()
    {
        var action = async () =>
        {
            await using var db = new MyDbContext(new DbContextOptionsBuilder()
                .UseSqlServer(_sql.GetConnectionString())
                .UseAutoConfigModel(op =>
                    op.ScanFrom(typeof(MyDbContext).Assembly).WithDefaultMappersType(typeof(Entity<>)))
                .Options);
            await db.Database.EnsureCreatedAsync();
        };
        await action.ShouldThrowAsync<InvalidOperationException>();
    }

    [TestMethod]
    public async Task TestWithCustomEntityMapperNullFilterBad()
    {
        var action = async () =>
        {
            await using var db = new MyDbContext(new DbContextOptionsBuilder()
                .UseSqlServer(_sql.GetConnectionString())
                .UseAutoConfigModel(op =>
                    op.ScanFrom(typeof(MyDbContext).Assembly).WithFilter(null!))
                .Options);
        };
        await action.ShouldThrowAsync<InvalidOperationException>();
    }
}