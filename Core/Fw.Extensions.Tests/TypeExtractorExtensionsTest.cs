using Fw.Extensions.Tests.TestObjects;
using DKNet.Fw.Extensions;

namespace Fw.Extensions.Tests;

[TestClass]
public class TestTypeExtractorExtensions
{
    [TestMethod]
    public void TestAbstract()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Abstract().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestDuplicateAssemblies()
    {
        // Arrange
        var assemblies = new[] { typeof(ITypeExtractor).Assembly, typeof(ITypeExtractor).Assembly };
        var types = assemblies.Extract().IsInstanceOf<ITypeExtractor>().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestInterface()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().IsInstanceOf<ITem>().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestNested()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Nested().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestNotClass()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().NotClass().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestNotEnum()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().NotEnum().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestExtract()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Publics().Classes().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [TestMethod]
    public void TestExtractGenericClass()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Generic().Classes().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThan(0);
    }

    [TestMethod]
    public void TestExtractNotInstanceOf()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Classes().NotInstanceOf<ITem>().ToList();

        // Act & Assert
        types.Contains(typeof(TestItem)).ShouldBeFalse();
        types.Contains(typeof(TestItem2)).ShouldBeFalse();
    }

    [TestMethod]
    public void TestExtractInstanceOfAny()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Classes().IsInstanceOfAny(typeof(ITem), typeof(IConfigItem))
            .ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [TestMethod]
    public void TestHasAttribute()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().HasAttribute<Attribute>().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestNotAbstract()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().NotAbstract().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }


    [TestMethod]
    public void TestNotGeneric()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().NotGeneric().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestNotInterface()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().NotInterface().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestNotNested()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().NotNested().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestNotPublic()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().NotPublic().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestPublic()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Publics().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestWherePredicate()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Where(t => t.IsClass && t.IsPublic).ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestExtractFromCollectionOfAssemblies()
    {
        // Arrange
        var assemblies = new List<Assembly> { typeof(ITypeExtractor).Assembly, typeof(TestEnumObject).Assembly };
        var types = assemblies.Extract().IsInstanceOf<ITypeExtractor>().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestExtractClasses()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Classes().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
        types.All(t => t.IsClass).ShouldBeTrue();
    }

    [TestMethod]
    public void TestExtractInterfaces()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Interfaces().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
        types.All(t => t.IsInterface).ShouldBeTrue();
    }

    [TestMethod]
    public void TestExtractEnums()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().Enums().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
        types.All(t => t.IsEnum).ShouldBeTrue();
        types.Contains(typeof(TestEnumObject)).ShouldBeTrue();
    }

    [TestMethod]
    public void TestExtractNonGenericTypes()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract().NotGeneric().Classes().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
        types.All(t => !t.IsGenericType).ShouldBeTrue();
    }

    // [TestMethod]
    // public void TestExtractWithoutAttribute()
    // {
    //     // Arrange
    //     var types = typeof(TestItem).Assembly.Extract().HasAttribute<ObsoleteAttribute>().ToList();
    //     // Act & Assert - Should have no items (since we don't have obsolete attributes)
    //     types.Count.ShouldBe(0);
    // }

    [TestMethod]
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
        types.All(t => t.IsClass && t.IsPublic && !t.IsAbstract && !t.IsGenericType).ShouldBeTrue();
    }

    [TestMethod]
    public void TestExtractWithCustomPredicate()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract()
            .Where(t => t.Name.StartsWith("Test"))
            .ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
        types.All(t => t.Name.StartsWith("Test")).ShouldBeTrue();
    }

    [TestMethod]
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
            type.IsImplementOf(typeof(ITem)).ShouldBeFalse();
            type.IsImplementOf(typeof(IDisposable)).ShouldBeFalse();
        }
    }

    [TestMethod]
    public void TestExtractFromMultipleAssembliesWithDuplicates()
    {
        // Arrange
        var assemblies = new[]
        {
            typeof(ITypeExtractor).Assembly,
            typeof(TestEnumObject).Assembly,
            typeof(ITypeExtractor).Assembly  // Duplicate
        };
        var types = assemblies.Extract().Classes().ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
        var uniqueTypes = types.Distinct().ToList();
        uniqueTypes.Count.ShouldBe(types.Count); // Should handle duplicates
    }

    [TestMethod]
    public void TestExtractStaticClasses()
    {
        // Arrange - Look for static classes (abstract + sealed)
        var types = typeof(ITypeExtractor).Assembly.Extract()
            .Classes()
            .Where(t => t.IsAbstract && t.IsSealed)
            .ToList();

        // Act & Assert
        types.Count.ShouldBeGreaterThanOrEqualTo(1);
        types.All(t => t.IsAbstract && t.IsSealed).ShouldBeTrue();
    }

    [TestMethod]
    public void TestExtractValueTypes()
    {
        // Arrange
        var types = typeof(TestEnumObject).Assembly.Extract()
            .Where(t => t.IsValueType && !t.IsEnum)
            .ToList();

        // Act & Assert - Should find structs
        types.All(t => t.IsValueType && !t.IsEnum).ShouldBeTrue();
    }
}