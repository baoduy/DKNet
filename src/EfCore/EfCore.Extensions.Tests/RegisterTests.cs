using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.Extensions.Tests;

public class RegisterTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private readonly MyDbContext _db = fixture.Db;


    [Fact]
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
                    OwnedEntity = new OwnedEntity { Name = "123" },
                    City = "HBD",
                    Street = "HBD"
                }
            }
        });

        await _db.SaveChangesAsync();

        (await _db.Set<User>().AnyAsync()).ShouldBeTrue();
        (await _db.Set<Address>().AnyAsync()).ShouldBeTrue();
    }

    [Fact]
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
                    OwnedEntity = new OwnedEntity { Name = "123" },
                    City = "HBD",
                    Street = "HBD"
                }
            }
        });

        await _db.SaveChangesAsync();

        var users = await _db.Set<User>().ToListAsync();
        var adds = await _db.Set<Address>().ToListAsync();

        (users.Count >= 1).ShouldBeTrue();
        (adds.Count >= 1).ShouldBeTrue();
    }

    [Fact]
    public async Task TestCreateDbCustomMapper()
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
                    OwnedEntity = new OwnedEntity { Name = "123" },
                    City = "HBD",
                    Street = "HBD"
                }
            }
        });

        await _db.SaveChangesAsync();

        (await _db.Set<Address>().AnyAsync()).ShouldBeTrue();
    }

    // [Fact]
    // public async Task TestEnumStatus1DataSeeding() =>
    //     (await _db.Set<EnumTables<EnumStatus1>>().CountAsync()).ShouldBe(3);
    //
    // [Fact]
    // public async Task TestEnumStatusDataSeeding()
    // {
    //     (await _db.Set<EnumTables<EnumStatus>>().CountAsync()).ShouldBe(3);
    //     var first = await _db.Set<EnumTables<EnumStatus>>().FirstAsync();
    //     first.Id.ShouldBeGreaterThanOrEqualTo(0);
    // }

    [Fact]
    public async Task TestIgnoredEntityAsync()
    {
        var action = () => _db.Set<IgnoredAutoMapperEntity>().ToListAsync();
        await action.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task TestWithCustomEntityMapperBad()
    {
        var action = async () =>
        {
            await using var db = new MyDbContext(new DbContextOptionsBuilder()
                .UseSqlServer(fixture.GetConnectionString("EfCoreDb"))
                .UseAutoConfigModel(op =>
                    op.ScanFrom(typeof(MyDbContext).Assembly).WithDefaultMappersType(typeof(Entity<>)))
                .Options);
            await db.Database.EnsureCreatedAsync();
        };
        await action.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task TestWithCustomEntityMapperNullFilterBad()
    {
        var action = async () =>
        {
            await using var db = new MyDbContext(new DbContextOptionsBuilder()
                .UseSqlServer(fixture.GetConnectionString("EfCoreDb"))
                .UseAutoConfigModel(op =>
                    op.ScanFrom(typeof(MyDbContext).Assembly).WithFilter(null!))
                .Options);
        };
        await action.ShouldThrowAsync<ArgumentNullException>();
    }
}