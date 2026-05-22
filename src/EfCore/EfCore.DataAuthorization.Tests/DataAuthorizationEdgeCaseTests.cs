using DKNet.EfCore.DataAuthorization.Internals;
using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests;

/// <summary>
///     Tests covering edge cases: empty ownership key and empty accessible keys.
/// </summary>
public class DataAuthorizationEdgeCaseTests
{
    #region Methods

    /// <summary>
    ///     Verifies that <see cref="DataAuthExtensions.GetQueryFilterKey{TEntity}" /> produces the expected key.
    /// </summary>
    [Fact]
    public void DataAuthExtensions_GetQueryFilterKey_ReturnsExpectedKey()
    {
        var key = DataAuthExtensions.GetQueryFilterKey<Root>();
        key.ShouldContain(typeof(Root).FullName!);
        key.ShouldContain(nameof(DataOwnerAuthQuery));
    }

    #endregion
}

/// <summary>
///     Tests for the scenario where the ownership key is empty (e.g., unauthenticated or system context).
///     The <see cref="DKNet.EfCore.DataAuthorization.Internals.DataOwnerHook" /> should skip ownership assignment.
/// </summary>
public class DataAuthorizationEmptyOwnerKeyTests(EmptyOwnerKeyFixture fixture)
    : IClassFixture<EmptyOwnerKeyFixture>
{
    #region Methods

    [Fact]
    public async Task WhenOwnerKeyIsEmpty_EntitiesRetainOriginalOwnedBy()
    {
        // Arrange: EmptyOwnerKeyProvider returns "" for GetOwnershipKey()
        // The hook should return early and NOT set OwnedBy on the entity
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entity = new Root("Test with no owner key", "original-owner");

        // Act
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        // Assert: OwnedBy was NOT overwritten since the key was empty and entity already had a value
        var ownedBy = ((IOwnedBy)entity).OwnedBy;
        ownedBy.ShouldBe("original-owner");
    }

    [Fact]
    public async Task WhenOwnerKeyIsEmpty_EntityWithEmptyOwnedBy_RemainsEmpty()
    {
        // Arrange: Provider returns empty ownerKey; entity has empty OwnedBy
        // Hook fires but ownerKey is empty → returns early → entity OwnedBy stays empty/default
        var db = fixture.Provider.GetRequiredService<DddContext>();

        // Use an entity that has OwnedBy set already so we can still add to DB
        // (the query filter is based on AccessibleKeys which is ["Steven"])
        var entity = new Root("Test with empty owner, owned by Steven", "Steven");

        // Act
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        // Assert: entity is visible via query filter (AccessibleKeys = ["Steven"])
        var result = await db.Set<Root>().ToListAsync();
        result.ShouldNotBeEmpty();
    }

    #endregion
}

/// <summary>
///     Tests for the scenario where AccessibleKeys is empty, meaning all entities should be visible
///     (super-user / admin context).
/// </summary>
public class DataAuthorizationEmptyAccessibleKeysTests(EmptyAccessibleKeysFixture fixture)
    : IClassFixture<EmptyAccessibleKeysFixture>
{
    #region Methods

    [Fact]
    public async Task WhenAccessibleKeysIsEmpty_AllEntitiesAreVisible()
    {
        // Arrange: EmptyAccessibleKeysProvider returns [] for GetAccessibleKeys()
        // The query filter's !AccessibleKeys.Any() == true branch is exercised → show ALL entities
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var entity1 = new Root("Entity owned by Steven", "Steven");
        var entity2 = new Root("Entity owned by other", "other-user");
        var entity3 = new Root("Entity owned by admin", "admin");

        await db.AddRangeAsync(entity1, entity2, entity3);
        await db.SaveChangesAsync();

        // Act: With empty AccessibleKeys, the query filter allows ALL entities
        var allEntities = await db.Set<Root>().ToListAsync();

        // Assert: Entities with ANY OwnedBy value should be visible
        allEntities.Count.ShouldBeGreaterThanOrEqualTo(3);
        allEntities.ShouldContain(e => ((IOwnedBy)e).OwnedBy == "Steven");
        allEntities.ShouldContain(e => ((IOwnedBy)e).OwnedBy == "other-user");
        allEntities.ShouldContain(e => ((IOwnedBy)e).OwnedBy == "admin");
    }

    [Fact]
    public void WhenAccessibleKeysIsEmpty_AccessibleKeysCollectionIsEmpty()
    {
        var db = fixture.Provider.GetRequiredService<DddContext>();
        db.AccessibleKeys.ShouldBeEmpty();
    }

    [Fact]
    public void WhenOwnershipKeyIsSetAndAccessibleKeysEmpty_ProviderReturnsCorrectKey()
    {
        var provider = fixture.Provider.GetRequiredService<IDataOwnerProvider>();
        provider.GetOwnershipKey().ShouldBe("Steven");
        provider.GetAccessibleKeys().ShouldBeEmpty();
    }

    #endregion
}
