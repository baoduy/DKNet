using DKNet.EfCore.Abstractions.Events;

namespace EfCore.Abstractions.Tests;

/// <summary>
///     DRK-676 §3 row 3 — <see cref="RaisesEventAttribute" />'s three constructor forms: type-naming,
///     label convention, and the new label-less convention form. Each ctor must set only the members that
///     belong to its own form and leave the others at their default (<see langword="null" />).
/// </summary>
public class RaisesEventAttributeTests
{
    #region Methods

    [Fact]
    public void TypeNamingCtor_SetsEventTypeOperationsAndProperties_LeavesLabelNull()
    {
        // Act
        var attribute = new RaisesEventAttribute(typeof(string), EventOperations.Created, "Name");

        // Assert
        attribute.EventType.ShouldBe(typeof(string));
        attribute.Label.ShouldBeNull();
        attribute.Operations.ShouldBe(EventOperations.Created);
        attribute.Properties.ShouldBe(["Name"]);
    }

    [Fact]
    public void LabelCtor_SetsLabelOperationsAndProperties_LeavesEventTypeNull()
    {
        // Act
        var attribute = new RaisesEventAttribute("Tier", EventOperations.Updated, "Status");

        // Assert
        attribute.Label.ShouldBe("Tier");
        attribute.EventType.ShouldBeNull();
        attribute.Operations.ShouldBe(EventOperations.Updated);
        attribute.Properties.ShouldBe(["Status"]);
    }

    [Fact]
    public void LabelLessCtor_SetsOperationsAndProperties_LeavesEventTypeAndLabelNull()
    {
        // Act
        var attribute = new RaisesEventAttribute(EventOperations.Created, "Name");

        // Assert
        attribute.EventType.ShouldBeNull();
        attribute.Label.ShouldBeNull();
        attribute.Operations.ShouldBe(EventOperations.Created);
        attribute.Properties.ShouldBe(["Name"]);
    }

    [Fact]
    public void LabelLessCtor_WithNoProperties_PropertiesIsEmpty()
    {
        // Act
        var attribute = new RaisesEventAttribute(EventOperations.Created | EventOperations.Deleted);

        // Assert
        attribute.Properties.ShouldBeEmpty();
        attribute.Operations.ShouldBe(EventOperations.Created | EventOperations.Deleted);
    }

    [Fact]
    public void AttributeUsage_AllowsMultipleOnClassOnly_NotInherited()
    {
        // Act
        var usage = typeof(RaisesEventAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        usage.ValidOn.ShouldBe(AttributeTargets.Class);
        usage.AllowMultiple.ShouldBeTrue();
        usage.Inherited.ShouldBeFalse();
    }

    [Fact]
    public void RaisesEventAttribute_IsSealed()
    {
        // Assert
        typeof(RaisesEventAttribute).IsSealed.ShouldBeTrue();
    }

    #endregion
}
