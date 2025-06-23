using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Configurations;
using DKNet.EfCore.Extensions.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Extensions.Tests;

[TestClass]
public class RegisterTests
{

    [TestMethod]
    public async Task TestRegisterEntitiesDefaultOptions()
    {
        //Create User with Address
        await UnitTestSetup.Db.Set<User>().AddAsync(new User("Duy")
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

        await UnitTestSetup.Db.SaveChangesAsync().ConfigureAwait(false);

        Assert.IsTrue(await UnitTestSetup.Db.Set<User>().CountAsync().ConfigureAwait(false) >= 1);
        Assert.IsTrue(await UnitTestSetup.Db.Set<Address>().CountAsync().ConfigureAwait(false) >= 1);
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
        await using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqliteMemory()
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
    }

    [TestMethod]

    //[ExpectedException(typeof(DbUpdateException))]
    public async Task TestCreateDbCustomMapper()
    {
        await using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqliteMemory()

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
    }

    [TestMethod]
    public void TestCreateDbNoAssembly()
    {
        using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqliteMemory()

            //No Assembly provided it will scan the MyDbContext assembly.
            .UseAutoConfigModel(op => op.ScanFrom()
                .WithDefaultMappersType(typeof(DefaultEntityTypeConfiguration<>)))
            .Options);
    }

    [TestMethod]
    public void TestCreateDbWithAssembly()
    {
        using var db = new MyDbContext(new DbContextOptionsBuilder<MyDbContext>()
            //No Assembly provided it will scan the MyDbContext assembly.
            .UseAutoConfigModel(op => op.ScanFrom(typeof(MyDbContext).Assembly))
            .UseSqliteMemory()
            .Options);
    }

    [TestMethod]

    //[ExpectedException(typeof(DbUpdateException))]
    public async Task TestCreateDbValidate()
    {
        await using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqliteMemory()
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
    }

    [TestMethod]
    public async Task TestEnumStatus1DataSeeding()
    {
        await using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqliteMemory()

            //No Assembly provided it will scan the MyDbContext assembly.
            .UseAutoConfigModel()
            .Options);
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
        (await db.Set<EnumTables<EnumStatus1>>().CountAsync().ConfigureAwait(false)).ShouldBe(3);
    }

    [TestMethod]
    public async Task TestEnumStatusDataSeeding()
    {
        await using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqliteMemory()

            //No Assembly provided it will scan the MyDbContext assembly.
            .UseAutoConfigModel()
            .Options);
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
        (await db.Set<EnumTables<EnumStatus>>().CountAsync().ConfigureAwait(false)).ShouldBe(3);

        var first = await db.Set<EnumTables<EnumStatus>>().FirstAsync().ConfigureAwait(false);
        first.Id.ShouldBeGreaterThanOrEqualTo(0);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public async Task TestIgnoredEntityAsync()
    {
        await using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqliteMemory()
            .UseAutoConfigModel(op =>
                op.ScanFrom(typeof(MyDbContext).Assembly)
                    .WithDefaultMappersType(typeof(DefaultEntityTypeConfiguration<>)))
            .Options);

        await db.Database.EnsureCreatedAsync();

        var list = await db.Set<IgnoredAutoMapperEntity>().ToListAsync();
    }

    [TestMethod]

    //[Ignore]
    [ExpectedException(typeof(InvalidOperationException))]
    public void TestWithCustomEntityMapperBad()
    {
        using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqliteMemory()
            .UseAutoConfigModel(op =>
                op.ScanFrom(typeof(MyDbContext).Assembly).WithDefaultMappersType(typeof(Entity<>)))
            .Options);

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void TestWithCustomEntityMapperNullFilterBad()
    {
        using var db = new MyDbContext(new DbContextOptionsBuilder()
            .UseSqliteMemory()
            .UseAutoConfigModel(op =>
                op.ScanFrom(typeof(MyDbContext).Assembly).WithFilter(null))
            .Options);
    }
}