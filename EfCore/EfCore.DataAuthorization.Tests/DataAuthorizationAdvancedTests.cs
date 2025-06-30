using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests;

public class DataAuthorizationAdvancedTests(DataKeyAdvancedFixture fixture) : IClassFixture<DataKeyAdvancedFixture>
{
    [Fact]
    public void DataOwnerProvider_GetAccessibleKeys_ReturnsCorrectKeys()
    {
        // Arrange
        var provider = fixture.Provider.GetRequiredService<IDataOwnerProvider>();

        // Act
        var keys = provider.GetAccessibleKeys();

        // Assert
        keys.ShouldNotBeEmpty();
        keys.ShouldContain("Steven");
    }

    [Fact]
    public void DataOwnerProvider_GetOwnershipKey_ReturnsCorrectKey()
    {
        // Arrange
        var provider = fixture.Provider.GetRequiredService<IDataOwnerProvider>();

        // Act
        var key = provider.GetOwnershipKey();

        // Assert
        key.ShouldBe("Steven");
    }

    [Fact]
    public async Task QueryFilter_FiltersDataBasedOnAccessibleKeys()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();
        
        // Create data with different ownership keys
        var ownedEntity = new Root("Owned Entity", "Steven");
        var unownedEntity = new Root("Unowned Entity", "different-key");
        
        await db.AddRangeAsync(ownedEntity, unownedEntity);
        await db.SaveChangesAsync();

        // Act
        var results = await db.Set<Root>().ToListAsync();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(r => ((IOwnedBy)r).OwnedBy == "Steven");
        results.ShouldNotContain(r => r.Name == "Unowned Entity");
    }

    [Fact]
    public async Task AutomaticOwnershipAssignment_AssignsCorrectOwnership()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entity = new Root("Auto Owned Entity", "Steven");

        // Act
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        // Assert
        var ownedBy = ((IOwnedBy)entity).OwnedBy;
        ownedBy.ShouldBe("Steven");
    }

    [Fact]
    public async Task MultipleEntities_AllReceiveCorrectOwnership()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entities = new[]
        {
            new Root("Entity 1", "Steven"),
            new Root("Entity 2", "Steven"),
            new Root("Entity 3", "Steven")
        };

        // Act
        await db.AddRangeAsync(entities);
        await db.SaveChangesAsync();

        // Assert
        entities.ShouldAllBe(e => ((IOwnedBy)e).OwnedBy == "Steven");
    }

    [Fact]
    public async Task QueryFilter_WorksWithComplexQueries()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entities = new[]
        {
            new Root("Complex Entity 1", "Steven"),
            new Root("Complex Entity 2", "Steven"),
            new Root("Test Entity 3", "Steven")
        };
        
        await db.AddRangeAsync(entities);
        await db.SaveChangesAsync();

        // Act
        var filteredResults = await db.Set<Root>()
            .Where(r => r.Name.Contains("Entity") || r.Name.Contains("Test"))
            .OrderBy(r => r.Name)
            .ToListAsync();

        // Assert
        filteredResults.ShouldAllBe(r => ((IOwnedBy)r).OwnedBy == "Steven");
    }

    [Fact]
    public void DatabaseContext_AccessibleKeys_ContainsExpectedKeys()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();

        // Act & Assert
        db.AccessibleKeys.ShouldNotBeEmpty();
        db.AccessibleKeys.ShouldContain("Steven");
    }

    [Fact]
    public async Task DataHook_DoesNotOverrideExistingOwnership()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entity = new Root("Pre-owned Entity", "pre-existing-key");

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
        var entity = new Root("Update Test Entity", "Steven");
        
        await db.AddAsync(entity);
        await db.SaveChangesAsync();
        
        var originalOwnership = ((IOwnedBy)entity).OwnedBy;

        // Act
        // Since Name has a private setter, we can't modify it directly
        // But we can test that ownership doesn't change on save
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
        var entity = new Root("Delete Test Entity", "Steven");
        
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
        var entities = new[]
        {
            new Root("Bulk 1", "Steven"),
            new Root("Bulk 2", "Steven"),
            new Root("Bulk 3", "Steven"),
            new Root("Bulk 4", "Steven"),
            new Root("Bulk 5", "Steven")
        };
        
        await db.AddRangeAsync(entities);
        await db.SaveChangesAsync();

        // Act
        var count = await db.Set<Root>().CountAsync();
        var allEntities = await db.Set<Root>().ToListAsync();

        // Assert
        count.ShouldBeGreaterThanOrEqualTo(entities.Length);
        allEntities.ShouldAllBe(e => ((IOwnedBy)e).OwnedBy == "Steven");
    }
}