using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests;

/// <summary>
///     Pins the full startup-error text for DRK-1897 shape (b), so the consumer always gets the remediation
///     (implement <see cref="IOwnedBy" /> on the hierarchy root) and not only the two type names.
/// </summary>
public class DataOwnerHierarchyFilterMessageTests
{
    #region Methods

    [Fact]
    public void TphUnownedRootWithOwnedDerived_ModelBuild_MessageTellsConsumerToMoveIOwnedByToRoot()
    {
        // Arrange
        using var context = new UnownedRootHierarchyDbContext(
            new DbContextOptionsBuilder<UnownedRootHierarchyDbContext>().UseSqlite("Data Source=:memory:").Options);

        // Act
        var ex = Record.Exception(() => context.Model);

        // Assert
        ex.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe(
            "Entity type 'OwnedInvoiceDocument' implements IOwnedBy but its inheritance hierarchy root " +
            "'UnownedDocument' does not. The data-owner query filter can only be applied to a hierarchy root; " +
            "implement IOwnedBy on 'UnownedDocument'.");
    }

    #endregion
}
