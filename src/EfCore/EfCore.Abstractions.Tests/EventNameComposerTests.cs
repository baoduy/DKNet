using System.Globalization;
using DKNet.EfCore.Abstractions.Events;

namespace EfCore.Abstractions.Tests;

/// <summary>
///     DRK-676 §3/§5 — <see cref="EventNameComposer" /> is the single source of the <c>[RaisesEvent]</c>
///     naming algorithm, shared (via <c>Compile Include</c>) by the build-time generator
///     (<c>RaisesEventValidator</c>) and the save-time runtime (<c>EventHook</c>). These tests exercise the
///     algorithm directly, independent of either consumer, including the §5 worked examples and the
///     culture-independence scenario.
/// </summary>
public class EventNameComposerTests
{
    #region Constants

    private const int Created = 1;
    private const int Updated = 2;
    private const int Deleted = 4;

    #endregion

    #region Methods

    [Theory]
    [InlineData(null, null, Created, "CustomerCreatedEvent")]
    [InlineData("Tier", null, Updated, "CustomerTierUpdatedEvent")]
    [InlineData(null, "Status", Updated, "CustomerStatusUpdatedEvent")]
    [InlineData(null, null, Created | Updated, "CustomerCreatedUpdatedEvent")]
    [InlineData(null, null, Created | Updated | Deleted, "CustomerCreatedUpdatedDeletedEvent")]
    public void Compose_WorkedExamplesFromSpec_MatchExactly(string? label, string? property, int operations, string expected)
    {
        // Arrange
        var properties = property is null ? null : new[] { property };

        // Act
        var composed = EventNameComposer.Compose("Customer", label, properties, operations);

        // Assert
        composed.ShouldBe(expected);
    }

    [Fact]
    public void Compose_LabelAndNarrowingProperty_LabelComesBeforeProperty()
    {
        // Act
        var composed = EventNameComposer.Compose("Customer", "Send", ["Email"], Updated);

        // Assert
        composed.ShouldBe("CustomerSendEmailUpdatedEvent");
    }

    [Fact]
    public void Compose_OperationsCombinedInAnyDeclarationOrder_AlwaysComposeCreatedUpdatedDeleted()
    {
        // Act - flags combined in reverse (Deleted | Created | Updated)
        var composed = EventNameComposer.Compose("Customer", null, null, Deleted | Created | Updated);

        // Assert
        composed.ShouldBe("CustomerCreatedUpdatedDeletedEvent");
    }

    [Fact]
    public void Compose_NarrowingPropertiesDeclaredInEitherOrder_ProduceTheIdenticalName()
    {
        // Act
        var statusThenName = EventNameComposer.Compose("Customer", null, ["Status", "Name"], Updated);
        var nameThenStatus = EventNameComposer.Compose("Order", null, ["Name", "Status"], Updated);

        // Assert
        statusThenName.ShouldBe("CustomerNameStatusUpdatedEvent");
        nameThenStatus.ShouldBe("OrderNameStatusUpdatedEvent");
    }

    [Fact]
    public void Compose_PropertyRepeatedInOneDeclaration_AppearsOnceInTheName()
    {
        // Act
        var composed = EventNameComposer.Compose("Customer", null, ["Status", "Status"], Updated);

        // Assert
        composed.ShouldBe("CustomerStatusUpdatedEvent");
    }

    [Fact]
    public void Compose_NarrowingWithoutUpdatedFlag_StillContributesToTheName()
    {
        // Act - R9: narrowing still composes into the name even when Updated is not declared
        var composed = EventNameComposer.Compose("Customer", null, ["Status"], Deleted);

        // Assert
        composed.ShouldBe("CustomerStatusDeletedEvent");
    }

    [Fact]
    public void Compose_NoLabelNoProperties_OmitsBothOptionalSegments()
    {
        // Act
        var composed = EventNameComposer.Compose("Customer", null, null, Created);

        // Assert
        composed.ShouldBe("CustomerCreatedEvent");
    }

    [Fact]
    public void Compose_EmptyLabel_TreatedAsAbsent()
    {
        // Act
        var composed = EventNameComposer.Compose("Customer", string.Empty, null, Created);

        // Assert
        composed.ShouldBe("CustomerCreatedEvent");
    }

    [Fact]
    public void Compose_WhitespaceOnlyLabel_ComposedVerbatim_NotTreatedAsAbsent()
    {
        // A whitespace-only label is present, not absent (per the composer's own XML doc) — composed
        // literally so the downstream identifier check (DKRAISEVT005) rejects it rather than the
        // composer silently falling back to an entity-only name nobody declared.
        // Act
        var composed = EventNameComposer.Compose("Customer", " ", null, Created);

        // Assert
        composed.ShouldBe("Customer CreatedEvent");
    }

    [Fact]
    public void Compose_PropertiesSortedOrdinal_NotCurrentCultureOrder()
    {
        // Act - ordinal: 'I' (0x49) sorts before 'i' (0x69), so "Id" precedes "id"
        var composed = EventNameComposer.Compose("Customer", null, ["id", "Id"], Updated);

        // Assert
        composed.ShouldBe("CustomerIdidUpdatedEvent");
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("sv-SE")]
    public void Compose_UnderNonInvariantCulture_MatchesTheInvariantResult(string cultureName)
    {
        // Arrange - the exact DRK-676 §5 culture scenario: narrowed to "Iban" and "Id"
        var invariant = EventNameComposer.Compose("Customer", null, ["Iban", "Id"], Updated);
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            // Act
            var underCulture = EventNameComposer.Compose("Customer", null, ["Iban", "Id"], Updated);

            // Assert
            underCulture.ShouldBe(invariant);
            underCulture.ShouldBe("CustomerIbanIdUpdatedEvent");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    #endregion
}
