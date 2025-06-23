using DKNet.Fw.Extensions;
using System.Collections.Generic;

namespace Fw.Extensions.Tests;

[TestClass]
public class TypeExtensionsTests
{
    [TestMethod]
    public void IsImplementOfSameTypeReturnsFalse()
    {
        // Arrange
        var type = typeof(string);

        // Act
        var result = type.IsImplementOf(type);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsImplementOfDerivedTypeReturnsTrue()
    {
        // Arrange
        var derivedType = typeof(List<int>);
        var baseType = typeof(IList<int>);

        // Act
        var result = derivedType.IsImplementOf(baseType);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsImplementOfInterfaceTypeReturnsTrue()
    {
        // Arrange
        var type = typeof(List<int>);
        var interfaceType = typeof(IEnumerable<int>);

        // Act
        var result = type.IsImplementOf(interfaceType);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsImplementOfNonMatchingTypeReturnsFalse()
    {
        // Arrange
        var type = typeof(string);
        var nonMatchingType = typeof(int);

        // Act
        var result = type.IsImplementOf(nonMatchingType);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsImplementOfNullTypeReturnsFalse()
    {
        // Arrange
        Type type = null;
        var matchingType = typeof(int);

        // Act
        var result = type.IsImplementOf(matchingType);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsImplementOfNullMatchingTypeReturnsFalse()
    {
        // Arrange
        var type = typeof(string);
        Type matchingType = null;

        // Act
        var result = type.IsImplementOf(matchingType);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsImplementOfGenericTypeReturnsTrue()
    {
        // Arrange
        var type = typeof(List<int>);
        var genericType = typeof(IList<>);

        // Act
        var result = type.IsImplementOf(genericType);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsNotNumericTypeNonNumericTypeReturnsTrue()
    {
        // Arrange
        object obj = "test";

        // Act
        var result = obj.IsNotNumericType();

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsNotNumericTypeNumericTypeReturnsFalse()
    {
        // Arrange
        object obj = 123;

        // Act
        var result = obj.IsNotNumericType();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsNumericTypeNumericTypeReturnsTrue()
    {
        // Arrange
        var type = typeof(int);

        // Act
        var result = type.IsNumericType();

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsNumericTypeNonNumericTypeReturnsFalse()
    {
        // Arrange
        var type = typeof(string);

        // Act
        var result = type.IsNumericType();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsNumericTypeNullTypeReturnsFalse()
    {
        // Arrange
        Type type = null;

        // Act
        var result = type.IsNumericType();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsNumericTypeNumericObjectReturnsTrue()
    {
        // Arrange
        object obj = 123.45;

        // Act
        var result = obj.IsNumericType();

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsNumericTypeNonNumericObjectReturnsFalse()
    {
        // Arrange
        object obj = "test";

        // Act
        var result = obj.IsNumericType();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsNumericTypeNullObjectReturnsFalse()
    {
        // Arrange
        object obj = null;

        // Act
        var result = obj.IsNumericType();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsImplementOfGenericTypeDefinitionReturnsTrue()
    {
        // Arrange
        var type = typeof(List<int>);
        var genericType = typeof(IList<>);

        // Act
        var result = type.IsImplementOf(genericType);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsImplementOfDerivedGenericTypeReturnsTrue()
    {
        // Arrange
        var type = typeof(List<int>);
        var baseType = typeof(IEnumerable<>);

        // Act
        var result = type.IsImplementOf(baseType);

        // Assert
        Assert.IsTrue(result);
    }
}