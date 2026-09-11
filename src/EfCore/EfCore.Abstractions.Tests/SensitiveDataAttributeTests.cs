// New tests to cover the [SensitiveData] role-gating constructor (DRK-1183).

namespace EfCore.Abstractions.Tests;

public class SensitiveDataAttributeTests
{
    #region Methods

    [Fact]
    public void Roles_DefaultsToEmpty_WhenNoRoleNamed()
    {
        // Arrange & Act
        var attribute = new SensitiveDataAttribute();

        // Assert
        attribute.Roles.ShouldNotBeNull();
        attribute.Roles.ShouldBeEmpty();
    }

    [Fact]
    public void Roles_ReflectsSingleRole_WhenOneRoleNamed()
    {
        // Arrange & Act
        var attribute = new SensitiveDataAttribute("pricing");

        // Assert
        attribute.Roles.ShouldBe(["pricing"]);
    }

    [Fact]
    public void Roles_ReflectsMultipleRoles_WhenSeveralRolesNamed()
    {
        // Arrange & Act
        var attribute = new SensitiveDataAttribute("pricing", "audit");

        // Assert
        attribute.Roles.ShouldBe(["pricing", "audit"]);
    }

    [Fact]
    public void AttributeUsage_StillTargetsPropertyOnly_AfterAddingRolesConstructor()
    {
        // Arrange
        var attributeType = typeof(SensitiveDataAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), true)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        attributeUsage.ShouldNotBeNull();
        attributeUsage.ValidOn.ShouldBe(AttributeTargets.Property);
        attributeUsage.Inherited.ShouldBeFalse();
    }

    [Fact]
    public void SensitiveDataAttribute_ShouldBeSealed()
    {
        // Arrange & Act
        var attributeType = typeof(SensitiveDataAttribute);

        // Assert
        attributeType.IsSealed.ShouldBeTrue();
    }

    #endregion
}
