using System.Text.Json;
using DKNet.EfCore.Extensions.Extensions;
using Xunit.Abstractions;

namespace EfCore.Repos.Tests;

public class RepoExtensionsTests(RepositoryFixture fixture, ITestOutputHelper output) : IClassFixture<RepositoryFixture>
{
    private async Task CreateUser(int number = 1)
    {
        var repo = fixture.DbContext;
        for (var i = 0; i < number; i++)
        {
            var newGuid = Guid.NewGuid();
            var entity = new UserGuid($"Test {newGuid}")
                { FirstName = $"Test {newGuid}", LastName = $"Test {newGuid}" };
            await repo.AddAsync(entity);
        }

        await repo.SaveChangesAsync();
        fixture.DbContext.ChangeTracker.Clear();
    }


    [Fact]
    public async Task GetPossibleNewNavigationEntitiesTest()
    {
        var db = fixture.DbContext;
        db.ChangeTracker.Clear();

        // Arrange
        await CreateUser(3);

        // Act
        var list = await db.Set<UserGuid>().ToListAsync();
        list.Count.ShouldBe(3);

        var user = list[^1];
        user.AddAddress(new AddressGuid { City = "Test City", Street = "Test Street", Country = "Test Country" });

        var nav = db.GetCollectionNavigations(db.Entry(user).Metadata.ClrType).ToList();
        nav.Count.ShouldBe(1, "It should have 1 navigations: Addresses");
        output.WriteLine("Navigation Count: " + string.Join(',', nav.Select(n => n.ClrType.Name)));

        var coll = user
            .GetNavigationValues(nav.First(n => n.ClrType == typeof(IReadOnlyCollection<AddressGuid>))).ToList();
        coll.Count.ShouldBe(1);

        output.WriteLine("AddressGuid is new: " + JsonSerializer.Serialize(coll[0]));
        db.Entry(coll[0]).IsNewEntity().ShouldBeTrue();

        var newEntities = db.GetNewEntitiesFromNavigations().ToList();
        newEntities.Count.ShouldBeGreaterThanOrEqualTo(1);

        (await db.AddNewEntitiesFromNavigations()).ShouldBeGreaterThanOrEqualTo(1);
        await db.SaveChangesAsync().ShouldNotThrowAsync();
    }
}