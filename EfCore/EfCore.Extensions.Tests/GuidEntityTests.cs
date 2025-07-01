
namespace EfCore.Extensions.Tests;


public class GuidEntityTests : SqlServerTestBase
{
    private static MyDbContext _db;

    
    public static async Task ClassSetup()
    {
        await StartSqlContainerAsync();
        _db = CreateDbContext("TestDb");
        await _db.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task TestCreateAsync()
    {
        var entity = new GuidEntity {Name = "Duy"};

        _db.Add(entity);
        await _db.SaveChangesAsync();
        entity.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task TestCreateAuditAsync()
    {
        var entity = new GuidAuditEntity {Name = "Duy"};

        _db.Add(entity);
        await _db.SaveChangesAsync();

        entity.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task TestUpdateAsync()
    {
        var entity = new GuidEntity {Name = "Duy"};
        var oldId = entity.Id.ToString();

        entity.Name = "Hoang";

        await _db.SaveChangesAsync();

        entity.Id.ToString().ShouldBe(oldId);
    }

    [Fact]
    public async Task TestUpdateAuditAsync()
    {
        var entity = new GuidAuditEntity {Name = "Duy"};
        var oldId = entity.Id.ToString();

        entity.Name = "Hoang";

        await _db.SaveChangesAsync();

        entity.Id.ToString().ShouldBe(oldId);
    }
    
}