using System.ComponentModel;

namespace Fw.Extensions.Tests;

public class TestTypeExtractorExtensions
{
    #region Methods

    [Fact]
    public void TestAbstract()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Abstract().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestChainedFiltering()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract()
            .Classes()
            .Publics()
            .NotAbstract()
            .NotGeneric()
            .ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
        types.TrueForAll(t => t.IsClass && t.IsPublic && !t.IsAbstract && !t.IsGenericType).ShouldBeTrue();
    }

    [Fact]
    public void TestDuplicateAssemblies()
    {
        // Arrange
        var assemblies = new[] { typeof(ITypeExtractor).Assembly, typeof(ITypeExtractor).Assembly };
        var types = assemblies.Extract().IsInstanceOf<ITypeExtractor>().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestExtract()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Publics().Classes().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void TestExtractClasses()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Classes().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
        types.TrueForAll(t => t.IsClass).ShouldBeTrue();
    }

    [Fact]
    public void TestExtractEnums()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Enums().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
        types.TrueForAll(t => t.IsEnum).ShouldBeTrue();
        types.Contains(typeof(TestEnumObject)).ShouldBeTrue();
    }

    [Fact]
    public void TestExtractFromCollectionOfAssemblies()
    {
        // Arrange
        var assemblies = new List<Assembly> { typeof(ITypeExtractor).Assembly, typeof(TestEnumObject).Assembly };
        var types = assemblies.Extract().IsInstanceOf<ITypeExtractor>().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestExtractFromMultipleAssembliesWithDuplicates()
    {
        // Arrange
        var assemblies = new[]
        {
            typeof(ITypeExtractor).Assembly,
            typeof(TestEnumObject).Assembly,
            typeof(ITypeExtractor).Assembly // Duplicate
        };
        var types = assemblies.Extract().Classes().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
        var uniqueTypes = types.Distinct().ToList();
        uniqueTypes.Count.ShouldBe(types.Count); // Should handle duplicates
    }

    [Fact]
    public void TestExtractGenericClass()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Generic().Classes().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void TestExtractInstanceOfAny()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Classes()
            .IsInstanceOfAny(typeof(ITem), typeof(IConfigItem))
            .ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void TestExtractInterfaces()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Interfaces().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
        types.TrueForAll(t => t.IsInterface).ShouldBeTrue();
    }

    [Fact]
    public void TestExtractNonGenericTypes()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().NotGeneric().Classes().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
        types.TrueForAll(t => !t.IsGenericType).ShouldBeTrue();
    }

    [Fact]
    public void TestExtractNotInstanceOf()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Classes().NotInstanceOf<ITem>().ToList();

        // Act & Assert
        types.Contains(typeof(TestItem)).ShouldBeFalse();
        types.Contains(typeof(TestItem2)).ShouldBeFalse();
    }

    [Fact]
    public void TestExtractNotInstanceOfAny()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract()
            .Classes()
            .NotInstanceOf<ITem>()
            .NotInstanceOf<IDisposable>()
            .ToList();

        // Act & Assert
        types.ShouldNotBeEmpty();
        foreach (var type in types)
        {
            type.IsAssignableTo(typeof(ITem)).ShouldBeFalse();
            type.IsAssignableTo(typeof(IDisposable)).ShouldBeFalse();
        }
    }

    [Fact]
    public void TestExtractStaticClasses()
    {
        // Arrange - Look for static classes (abstract + sealed)
        var types = typeof(ITypeExtractor).Assembly.Extract()
            .Classes()
            .Where(t => t.IsAbstract && t.IsSealed)
            .ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
        types.TrueForAll(t => t.IsAbstract && t.IsSealed).ShouldBeTrue();
    }

    [Fact]
    public void TestExtractValueTypes()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract()
            .Where(t => t.IsValueType && !t.IsEnum)
            .ToList();

        // Act & Assert - Should find structs
        types.TrueForAll(t => t.IsValueType && !t.IsEnum).ShouldBeTrue();
    }

    [Fact]
    public void TestExtractWithCustomPredicate()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract()
            .Where(t => t.Name.StartsWith("Test"))
            .ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
        types.TrueForAll(t => t.Name.StartsWith("Test", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
    }

    [Fact]
    public void TestExtractWithoutAttribute()
    {
        // Arrange
        var types = typeof(TestItem).Assembly.Extract().HasAttribute<BrowsableAttribute>().ToList();
        // Act & Assert - Should have no items (since we don't have obsolete attributes)
        types.Count.ShouldBe(0);
    }

    [Fact]
    public void TestHasAttribute()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().HasAttribute<Attribute>().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestInterface()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().IsInstanceOf<ITem>().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestNested()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Nested().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestNotAbstract()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().NotAbstract().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestNotClass()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().NotClass().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestNotEnum()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().NotEnum().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }


    [Fact]
    public void TestNotGeneric()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().NotGeneric().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestNotInterface()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().NotInterface().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestNotNested()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().NotNested().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestNotPublic()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().NotPublic().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestPublic()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Publics().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestWherePredicate()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Where(t => t.IsClass && t.IsPublic).ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    #endregion
}