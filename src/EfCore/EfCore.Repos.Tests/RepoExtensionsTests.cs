using DKNet.EfCore.Repos;
using EfCore.Repos.Tests.TestEntities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace EfCore.Repos.Tests;

public class RepoExtensionsTests(RepositoryFixture fixture) : IClassFixture<RepositoryFixture>
{
    private async Task CreateUser(int number = 1)
    {
        var repo = fixture.Repository;
        for (var i = 0; i < number; i++)
        {
            var newGuid = Guid.NewGuid();
            var entity = new User($"Test {newGuid}") { FirstName = $"Test {newGuid}", LastName = $"Test {newGuid}" };
            await repo.AddAsync(entity);
        }

        await repo.SaveChangesAsync();
        fixture.DbContext.ChangeTracker.Clear();
    }


    [Fact]
    public async Task GetPossibleNewNavigationEntitiesTest()
    {
        var repo = fixture.Repository;
        // Arrange
        await CreateUser(3);

        // Act
        var list = await repo.Gets().ToListAsync();
        list.Count.ShouldBe(3);

        var user = list[^1];
        user.AddAddress(new Address { City = "Test City", Street = "Test Street", Country = "Test Country" });

        var possibleChanges = fixture.DbContext.GetPossibleUpdatingEntities().ToList();
        possibleChanges.Count.ShouldBe(3);

        var nav = fixture.DbContext.GetCollectionNavigations<User>().ToList();
        nav.Count.ShouldBe(1);
        var entities = user.GetNavigationEntities(nav[0]).ToList();
        entities.Count.ShouldBe(1);
        fixture.DbContext.Entry(entities[0]).IsNewEntity().ShouldBeTrue();

        var coll = fixture.DbContext.GetNewEntitiesFromNavigations(user).ToList();
        coll.Count.ShouldBe(1);

        await repo.SaveChangesAsync().ShouldNotThrowAsync();
    }
}