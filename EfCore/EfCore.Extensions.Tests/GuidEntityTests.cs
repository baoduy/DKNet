
namespace EfCore.Extensions.Tests;

[TestClass]
public class GuidEntityTests : SqlServerTestBase
{
    private static MyDbContext _db;

    [ClassInitialize]
    public static async Task ClassSetup(TestContext _)
    {
        await StartSqlContainerAsync();
        _db = CreateDbContext("TestDb");
        await _db.Database.EnsureCreatedAsync();
    }

    [TestMethod]
    public async Task TestCreateAsync()
    {
        var entity = new GuidEntity {Name = "Duy"};

        _db.Add(entity);
        await _db.SaveChangesAsync();
        entity.Id.ShouldNotBe(Guid.Empty);
    }

    [TestMethod]
    public async Task TestCreateAuditAsync()
    {
        var entity = new GuidAuditEntity {Name = "Duy"};

        _db.Add(entity);
        await _db.SaveChangesAsync();

        entity.Id.ShouldNotBe(Guid.Empty);
    }

    [TestMethod]
    public async Task TestUpdateAsync()
    {
        var entity = new GuidEntity {Name = "Duy"};
        var oldId = entity.Id.ToString();

        entity.Name = "Hoang";

        await _db.SaveChangesAsync();

        entity.Id.ToString().ShouldBe(oldId);
    }

    [TestMethod]
    public async Task TestUpdateAuditAsync()
    {
        var entity = new GuidAuditEntity {Name = "Duy"};
        var oldId = entity.Id.ToString();

        entity.Name = "Hoang";

        await _db.SaveChangesAsync();

        entity.Id.ToString().ShouldBe(oldId);
    }
    
}