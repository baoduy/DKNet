using Fw.Extensions.Tests.TestObjects;

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
}