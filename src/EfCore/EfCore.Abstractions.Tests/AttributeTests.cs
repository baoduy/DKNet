namespace EfCore.Abstractions.Tests;

public class AttributeTests
{
    #region Methods

    [Fact]
    public void IgnoreEntityMapperAttribute_ShouldBeApplicableToClass()
    {
        // Arrange
        var attributeType = typeof(IgnoreEntityAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), true)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        attributeUsage.ShouldNotBeNull();
        attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
        attributeUsage.Inherited.ShouldBeFalse();
    }

    [Fact]
    public void IgnoreEntityMapperAttribute_ShouldBeSealed()
    {
        // Arrange & Act
        var attributeType = typeof(IgnoreEntityAttribute);

        // Assert
        attributeType.IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void IgnoreEntityMapperAttribute_ShouldInheritFromAttribute()
    {
        // Arrange & Act
        var attribute = new IgnoreEntityAttribute();

        // Assert
        attribute.ShouldBeAssignableTo<Attribute>();
    }

    [Fact]
    public void SqlSequenceAttribute_ConstructorWithSchema_ShouldSetSchema()
    {
        // Arrange
        const string expectedSchema = "custom_schema";

        // Act
        var attribute = new SqlSequenceAttribute(expectedSchema);

        // Assert
        attribute.Schema.ShouldBe(expectedSchema);
    }

    [Fact]
    public void SqlSequenceAttribute_DefaultConstructor_ShouldUseDefaultSchema()
    {
        // Act
        var attribute = new SqlSequenceAttribute();

        // Assert
        attribute.Schema.ShouldBe("seq");
    }

    [Fact]
    public void SqlSequenceAttribute_ShouldBeApplicableToEnum()
    {
        // Arrange
        var attributeType = typeof(SqlSequenceAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), true)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        attributeUsage.ShouldNotBeNull();
        attributeUsage.ValidOn.ShouldBe(AttributeTargets.Enum);
    }

    [Fact]
    public void SqlSequenceAttribute_ShouldBeSealed()
    {
        // Arrange & Act
        var attributeType = typeof(SqlSequenceAttribute);

        // Assert
        attributeType.IsSealed.ShouldBeTrue();
    }

    #endregion

    // [Fact]
    // public void StaticDataAttribute_ConstructorWithName_ShouldSetName()
    // {
    //     // Arrange
    //     const string expectedName = "test_table";
    //
    //     // Act
    //     var attribute = new StaticDataAttribute(expectedName);
    //
    //     // Assert
    //     attribute.Name.ShouldBe(expectedName);
    // }
    //
    // [Fact]
    // public void StaticDataAttribute_ShouldBeApplicableToEnum()
    // {
    //     // Arrange
    //     var attributeType = typeof(StaticDataAttribute);
    //
    //     // Act
    //     var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), true)
    //         .Cast<AttributeUsageAttribute>()
    //         .FirstOrDefault();
    //
    //     // Assert
    //     attributeUsage.ShouldNotBeNull();
    //     attributeUsage.ValidOn.ShouldBe(AttributeTargets.Enum);
    // }

    // [Fact]
    // public void StaticDataAttribute_ShouldBeSealed()
    // {
    //     // Arrange & Act
    //     var attributeType = typeof(StaticDataAttribute);
    //
    //     // Assert
    //     attributeType.IsSealed.ShouldBeTrue();
    // }
    //
    // [Fact]
    // public void StaticDataAttribute_ShouldInheritFromTableAttribute()
    // {
    //     // Arrange & Act
    //     var attribute = new StaticDataAttribute("test");
    //
    //     // Assert
    //     attribute.ShouldBeAssignableTo<TableAttribute>();
    // }
}