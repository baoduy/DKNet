using DKNet.EfCore.DtoGenerator;
using Shouldly;

namespace EfCore.DtoGenerator.Tests;

public class GenerateDtoAttributeTests
{
    #region Methods

    [Fact]
    public void Constructor_SetsEntityType()
    {
        // Arrange
        var entityType = typeof(TestEntity);

        // Act
        var attribute = new GenerateDtoAttribute(entityType);

        // Assert
        attribute.EntityType.ShouldBe(entityType);
    }

    [Fact]
    public void EntityFullName_ReturnsFullName()
    {
        // Arrange
        var entityType = typeof(TestEntity);
        var attribute = new GenerateDtoAttribute(entityType);

        // Act
        var fullName = attribute.EntityFullName;

        // Assert
        fullName.ShouldNotBeNullOrEmpty();
        fullName.ShouldContain("TestEntity");
    }

    [Fact]
    public void EntityFullName_WithTypeWithoutFullName_ReturnsName()
    {
        // Arrange
        var attribute = new GenerateDtoAttribute(typeof(TestEntity));

        // Act
        var fullName = attribute.EntityFullName;

        // Assert
        fullName.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Exclude_CanBeSet()
    {
        // Arrange
        var attribute = new GenerateDtoAttribute(typeof(TestEntity));
        var excludeProperties = new[] { "Id", "CreatedAt" };

        // Act
        attribute.Exclude = excludeProperties;

        // Assert
        attribute.Exclude.ShouldBe(excludeProperties);
    }

    [Fact]
    public void Exclude_DefaultsToEmptyArray()
    {
        // Arrange
        var attribute = new GenerateDtoAttribute(typeof(TestEntity));

        // Act & Assert
        attribute.Exclude.ShouldNotBeNull();
        attribute.Exclude.ShouldBeEmpty();
    }

    [Fact]
    public void IgnoreComplexType_CanBeSet()
    {
        // Arrange
        var attribute = new GenerateDtoAttribute(typeof(TestEntity));

        // Act
        attribute.IgnoreComplexType = true;

        // Assert
        attribute.IgnoreComplexType.ShouldBeTrue();
    }

    [Fact]
    public void IgnoreComplexType_DefaultsToFalse()
    {
        // Arrange
        var attribute = new GenerateDtoAttribute(typeof(TestEntity));

        // Act & Assert
        attribute.IgnoreComplexType.ShouldBeFalse();
    }

    [Fact]
    public void Include_CanBeSet()
    {
        // Arrange
        var attribute = new GenerateDtoAttribute(typeof(TestEntity));
        var includeProperties = new[] { "Name", "Email" };

        // Act
        attribute.Include = includeProperties;

        // Assert
        attribute.Include.ShouldBe(includeProperties);
    }

    [Fact]
    public void Include_DefaultsToEmptyArray()
    {
        // Arrange
        var attribute = new GenerateDtoAttribute(typeof(TestEntity));

        // Act & Assert
        attribute.Include.ShouldNotBeNull();
        attribute.Include.ShouldBeEmpty();
    }

    #endregion

    public class TestEntity
    {
        #region Properties

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        #endregion
    }
}