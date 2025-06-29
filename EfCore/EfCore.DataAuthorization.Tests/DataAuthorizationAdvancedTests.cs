using DKNet.EfCore.DataAuthorization;
using Microsoft.EntityFrameworkCore;
using EfCore.DataAuthorization.Tests.TestEntities;

namespace EfCore.DataAuthorization.Tests;

public class DataAuthorizationAdvancedTests(DataKeyAdvancedFixture fixture) : IClassFixture<DataKeyAdvancedFixture>
{
    [Fact]
    public async Task DataOwnerProvider_GetAccessibleKeys_ReturnsCorrectKeys()
    {
        // Arrange
        var provider = fixture.Provider.GetRequiredService<IDataOwnerProvider>();

        // Act
        var keys = provider.GetAccessibleKeys();

        // Assert
        keys.ShouldNotBeEmpty();
        keys.ShouldContain(TestDataKeyProvider.TestKey);
    }

    [Fact]
    public void DataOwnerProvider_GetOwnershipKey_ReturnsCorrectKey()
    {
        // Arrange
        var provider = fixture.Provider.GetRequiredService<IDataOwnerProvider>();

        // Act
        var key = provider.GetOwnershipKey();

        // Assert
        key.ShouldBe(TestDataKeyProvider.TestKey);
    }

    [Fact]
    public async Task QueryFilter_FiltersDataBasedOnAccessibleKeys()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();
        
        // Create data with different ownership keys
        var ownedEntity = new Root { Name = "Owned Entity" };
        var unownedEntity = new Root { Name = "Unowned Entity" };
        
        // Manually set ownership to bypass the hook for testing
        typeof(Root).GetProperty("OwnedBy")?.SetValue(ownedEntity, TestDataKeyProvider.TestKey);
        typeof(Root).GetProperty("OwnedBy")?.SetValue(unownedEntity, "different-key");
        
        await db.AddRangeAsync(ownedEntity, unownedEntity);
        await db.SaveChangesAsync();

        // Act
        var results = await db.Set<Root>().ToListAsync();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(r => ((IOwnedBy)r).OwnedBy == TestDataKeyProvider.TestKey);
        results.ShouldNotContain(r => r.Name == "Unowned Entity");
    }

    [Fact]
    public async Task AutomaticOwnershipAssignment_AssignsCorrectOwnership()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entity = new Root { Name = "Auto Owned Entity" };

        // Act
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        // Assert
        var ownedBy = ((IOwnedBy)entity).OwnedBy;
        ownedBy.ShouldBe(TestDataKeyProvider.TestKey);
    }

    [Fact]
    public async Task MultipleEntities_AllReceiveCorrectOwnership()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entities = new[]
        {
            new Root { Name = "Entity 1" },
            new Root { Name = "Entity 2" },
            new Root { Name = "Entity 3" }
        };

        // Act
        await db.AddRangeAsync(entities);
        await db.SaveChangesAsync();

        // Assert
        entities.ShouldAllBe(e => ((IOwnedBy)e).OwnedBy == TestDataKeyProvider.TestKey);
    }

    [Fact]
    public async Task QueryFilter_WorksWithComplexQueries()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entities = AutoFaker.Generate<Root>(10);
        
        await db.AddRangeAsync(entities);
        await db.SaveChangesAsync();

        // Act
        var filteredResults = await db.Set<Root>()
            .Where(r => r.Name.Contains("Entity") || r.Name.Contains("Test"))
            .OrderBy(r => r.Name)
            .ToListAsync();

        // Assert
        filteredResults.ShouldAllBe(r => ((IOwnedBy)r).OwnedBy == TestDataKeyProvider.TestKey);
    }

    [Fact]
    public async Task DatabaseContext_AccessibleKeys_ContainsExpectedKeys()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();

        // Act & Assert
        db.AccessibleKeys.ShouldNotBeEmpty();
        db.AccessibleKeys.ShouldContain(TestDataKeyProvider.TestKey);
    }

    [Fact]
    public async Task DataHook_DoesNotOverrideExistingOwnership()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entity = new Root { Name = "Pre-owned Entity" };
        
        // Set ownership before adding to context
        typeof(Root).GetProperty("OwnedBy")?.SetValue(entity, "pre-existing-key");

        // Act
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        // Verify the ownership wasn't changed by the hook
        var ownedBy = ((IOwnedBy)entity).OwnedBy;
        ownedBy.ShouldBe("pre-existing-key");
    }

    [Fact]
    public async Task UpdateOperations_DoNotChangeOwnership()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entity = new Root { Name = "Update Test Entity" };
        
        await db.AddAsync(entity);
        await db.SaveChangesAsync();
        
        var originalOwnership = ((IOwnedBy)entity).OwnedBy;

        // Act
        entity.Name = "Updated Name";
        await db.SaveChangesAsync();

        // Assert
        var currentOwnership = ((IOwnedBy)entity).OwnedBy;
        currentOwnership.ShouldBe(originalOwnership);
    }

    [Fact]
    public async Task DeleteOperations_WorkCorrectlyWithFiltering()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entity = new Root { Name = "Delete Test Entity" };
        
        await db.AddAsync(entity);
        await db.SaveChangesAsync();
        
        var entityId = entity.Id;

        // Act
        db.Remove(entity);
        await db.SaveChangesAsync();

        // Assert
        var result = await db.Set<Root>().FindAsync(entityId);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task BulkOperations_RespectDataFiltering()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entities = AutoFaker.Generate<Root>(5);
        
        await db.AddRangeAsync(entities);
        await db.SaveChangesAsync();

        // Act
        var count = await db.Set<Root>().CountAsync();
        var allEntities = await db.Set<Root>().ToListAsync();

        // Assert
        count.ShouldBe(entities.Count);
        allEntities.ShouldAllBe(e => ((IOwnedBy)e).OwnedBy == TestDataKeyProvider.TestKey);
    }
}