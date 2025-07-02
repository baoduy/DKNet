namespace EfCore.DataAuthorization.Tests;

public class DataKeyTests(DataKeyFixture fixture) : IClassFixture<DataKeyFixture>
{
    [Fact]
    public void TestContextDataKeys()
    {
        //Create account for Key
        var db = fixture.Provider.GetRequiredService<DddContext>();
        db.AccessibleKeys.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task TestDataKeys()
    {
        //Create account for Key
        var provider = fixture.Provider.GetRequiredService<IDataOwnerProvider>();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var accounts1 = AutoFaker.Generate<Root>(100);

        await db.AddRangeAsync(accounts1);
        await db.SaveChangesAsync();

        //Verify Key
        db.Set<Root>().ToList()
            .All(a => string.Equals(((IOwnedBy)a).OwnedBy, provider.GetOwnershipKey(), System.StringComparison.OrdinalIgnoreCase))
            .ShouldBeTrue();
    }

    [Fact]
    public void TestDataKeysSetup()
    {
        //Create account for Key
        var db = fixture.Provider.GetRequiredService<DddContext>();
        db.Database.EnsureCreated();

        var etype = db.Model.GetEntityTypes().FirstOrDefault(e => e.ClrType == typeof(Root));
        etype.ShouldNotBeNull();
        etype.GetQueryFilter().ShouldNotBeNull();
    }
}