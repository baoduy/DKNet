using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests;

/// <summary>
///     Acceptance tests for DRK-1897 (Proposed solution step 1): an EF Core inheritance hierarchy must never fall
///     out of the data-owner query filter.
///     <list type="bullet">
///         <item>
///             Shape (a): a concrete <see cref="IOwnedBy" /> TPH root (<see cref="OwnedPayment" />) with a derived
///             type (<see cref="OwnedCardPayment" />) — both must be filtered. The caller's accessible key is
///             <c>Steven</c> (<see cref="TestDataKeyProvider" />); rows owned by <c>Mallory</c> must stay invisible.
///         </item>
///         <item>
///             Shape (b): a TPH root that is not <see cref="IOwnedBy" /> (<see cref="UnownedDocument" />) with an
///             <see cref="IOwnedBy" /> derived type (<see cref="OwnedInvoiceDocument" />) — model build must throw.
///         </item>
///     </list>
/// </summary>
public class DataOwnerHierarchyFilterTests(DataKeyFixture fixture) : IClassFixture<DataKeyFixture>, IAsyncLifetime
{
    #region Methods

    public Task DisposeAsync() => Task.CompletedTask;

    public async Task InitializeAsync()
    {
        using var scope = fixture.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DddContext>();
        if (await db.Set<OwnedPayment>().IgnoreQueryFilters().AnyAsync()) return;

        db.AddRange(
            new OwnedPayment("steven-payment", "Steven"),
            new OwnedCardPayment("steven-card-payment", "1111", "Steven"),
            new OwnedPayment("mallory-payment", "Mallory"),
            new OwnedCardPayment("mallory-card-payment", "2222", "Mallory"));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task TphOwnedRoot_QueryRootSet_ReturnsOnlyCallerOwnedRootAndDerivedRows()
    {
        // Arrange
        using var scope = fixture.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DddContext>();

        // Precondition: all four rows exist, so an empty result could only come from the filter
        var allReferences = await db.Set<OwnedPayment>().IgnoreQueryFilters()
            .Select(p => p.Reference).OrderBy(r => r).ToListAsync();
        allReferences.ShouldBe(
            ["mallory-card-payment", "mallory-payment", "steven-card-payment", "steven-payment"]);

        // Act
        var visible = await db.Set<OwnedPayment>().OrderBy(p => p.Reference).ToListAsync();

        // Assert
        visible.Select(p => p.Reference).ShouldBe(["steven-card-payment", "steven-payment"]);
        visible.Select(p => p.OwnedBy).ShouldBe(["Steven", "Steven"]);
    }

    [Fact]
    public async Task TphOwnedRoot_QueryDerivedSet_ReturnsOnlyCallerOwnedDerivedRows()
    {
        // Arrange
        using var scope = fixture.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DddContext>();

        // Precondition: both derived rows exist
        var allReferences = await db.Set<OwnedCardPayment>().IgnoreQueryFilters()
            .Select(p => p.Reference).OrderBy(r => r).ToListAsync();
        allReferences.ShouldBe(["mallory-card-payment", "steven-card-payment"]);

        // Act
        var visible = await db.Set<OwnedCardPayment>().ToListAsync();

        // Assert
        visible.Select(p => p.Reference).ShouldBe(["steven-card-payment"]);
        visible.Select(p => p.OwnedBy).ShouldBe(["Steven"]);
    }

    [Fact]
    public async Task TphOwnedRoot_QueryAsCallerWithoutMalloryKey_SeesZeroMalloryRowsInRootAndDerivedSets()
    {
        // Arrange
        using var scope = fixture.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DddContext>();

        // Act
        var rootSetMalloryRows = await db.Set<OwnedPayment>().CountAsync(p => p.OwnedBy == "Mallory");
        var derivedSetMalloryRows = await db.Set<OwnedCardPayment>().CountAsync(p => p.OwnedBy == "Mallory");

        // Assert
        rootSetMalloryRows.ShouldBe(0);
        derivedSetMalloryRows.ShouldBe(0);
    }

    [Fact]
    public void TphOwnedRoot_ModelBuild_RootDeclaresOwnerFilterAndDerivedInheritsIt()
    {
        // Arrange
        using var scope = fixture.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DddContext>();

        // Act
        var root = db.Model.FindEntityType(typeof(OwnedPayment));
        var derived = db.Model.FindEntityType(typeof(OwnedCardPayment));

        // Assert: EF Core only accepts a query filter on a hierarchy root and applies it to every derived type
        root.ShouldNotBeNull();
        derived.ShouldNotBeNull();
        derived.BaseType.ShouldBe(root);
        root.GetDeclaredQueryFilters().Count.ShouldBe(1);
        derived.GetDeclaredQueryFilters().Count.ShouldBe(0);
    }

    [Fact]
    public void TphUnownedRootWithOwnedDerived_ModelBuild_ThrowsInvalidOperationExceptionNamingBothTypes()
    {
        // Arrange: DbContext.Model lazily triggers OnModelCreating, which applies DataOwnerAuthQuery
        using var context = new UnownedRootHierarchyDbContext(
            new DbContextOptionsBuilder<UnownedRootHierarchyDbContext>().UseSqlite("Data Source=:memory:").Options);

        // Act
        var ex = Record.Exception(() => context.Model);

        // Assert: fail closed with the real exception, not wrapped by GlobalQueryFilter.Apply's reflection Invoke
        ex.ShouldNotBeNull();
        ex.ShouldBeOfType<InvalidOperationException>();
        ex.Message.ShouldContain("OwnedInvoiceDocument");
        ex.Message.ShouldContain("UnownedDocument");
        ex.Message.ShouldNotContain("IDataOwnerDbContext");
    }

    #endregion
}
