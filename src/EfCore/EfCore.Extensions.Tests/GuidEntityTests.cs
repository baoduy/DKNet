namespace EfCore.Extensions.Tests;

public class GuidEntityTests(MemoryFixture fixture) : IClassFixture<MemoryFixture>
{
    #region Fields

    private readonly MyDbContext _db = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public async Task TestCreateAsync()
    {
        var entity = new GuidEntity { Name = "Duy" };

        this._db.Add(entity);
        await this._db.SaveChangesAsync();
        entity.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task TestCreateAuditAsync()
    {
        var entity = new GuidAuditEntity { Name = "Duy" };

        this._db.Add(entity);
        await this._db.SaveChangesAsync();

        entity.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task TestUpdateAsync()
    {
        var entity = new GuidEntity { Name = "Duy" };
        var oldId = entity.Id.ToString();

        entity.Name = "Hoang";

        await this._db.SaveChangesAsync();

        entity.Id.ToString().ShouldBe(oldId);
    }

    [Fact]
    public async Task TestUpdateAuditAsync()
    {
        var entity = new GuidAuditEntity { Name = "Duy" };
        var oldId = entity.Id.ToString();

        entity.Name = "Hoang";

        await this._db.SaveChangesAsync();

        entity.Id.ToString().ShouldBe(oldId);
    }

    #endregion
}