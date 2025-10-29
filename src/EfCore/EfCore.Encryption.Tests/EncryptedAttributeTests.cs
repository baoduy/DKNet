using DKNet.EfCore.Encryption.Attributes;
using Shouldly;

namespace EfCore.Encryption.Tests;

public class EncryptedAttributeTests
{
    #region Methods

    [Fact]
    public void EncryptedAttribute_ShouldBeApplicableToProperty()
    {
        // Arrange
        var attributeType = typeof(EncryptedAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), true)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        attributeUsage.ShouldNotBeNull();
        attributeUsage.ValidOn.ShouldBe(AttributeTargets.Property);
    }

    [Fact]
    public void EncryptedAttribute_ShouldBeInstantiable()
    {
        // Act
        var attribute = new EncryptedAttribute();

        // Assert
        attribute.ShouldNotBeNull();
        attribute.ShouldBeOfType<EncryptedAttribute>();
    }

    [Fact]
    public void EncryptedAttribute_ShouldBeSealed()
    {
        // Arrange & Act
        var attributeType = typeof(EncryptedAttribute);

        // Assert
        attributeType.IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void EncryptedAttribute_ShouldInheritFromAttribute()
    {
        // Arrange & Act
        var attribute = new EncryptedAttribute();

        // Assert
        attribute.ShouldBeAssignableTo<Attribute>();
    }

    #endregion
}