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

    [TestMethod]
    public void IsImplementOfGenericMethodReturnsCorrectResult()
    {
        // Arrange
        var type = typeof(List<int>);

        // Act
        var result = type.IsImplementOf<IEnumerable<int>>();

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsImplementOfNonGenericBaseClassReturnsTrue()
    {
        // Arrange
        var type = typeof(ArgumentException);
        var baseType = typeof(Exception);

        // Act
        var result = type.IsImplementOf(baseType);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsImplementOfGenericBaseClassReturnsTrue()
    {
        // Arrange
        var type = typeof(List<int>);
        var baseType = typeof(ICollection<>);

        // Act
        var result = type.IsImplementOf(baseType);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsNumericTypeAllNumericTypesReturnTrue()
    {
        // Arrange & Act & Assert
        Assert.IsTrue(typeof(byte).IsNumericType());
        Assert.IsTrue(typeof(sbyte).IsNumericType());
        Assert.IsTrue(typeof(ushort).IsNumericType());
        Assert.IsTrue(typeof(short).IsNumericType());
        Assert.IsTrue(typeof(uint).IsNumericType());
        Assert.IsTrue(typeof(int).IsNumericType());
        Assert.IsTrue(typeof(ulong).IsNumericType());
        Assert.IsTrue(typeof(long).IsNumericType());
        Assert.IsTrue(typeof(float).IsNumericType());
        Assert.IsTrue(typeof(double).IsNumericType());
        Assert.IsTrue(typeof(decimal).IsNumericType());
    }

    [TestMethod]
    public void IsNumericTypeAllNonNumericTypesReturnFalse()
    {
        // Arrange & Act & Assert
        Assert.IsFalse(typeof(string).IsNumericType());
        Assert.IsFalse(typeof(bool).IsNumericType());
        Assert.IsFalse(typeof(char).IsNumericType());
        Assert.IsFalse(typeof(DateTime).IsNumericType());
        Assert.IsFalse(typeof(object).IsNumericType());
        Assert.IsFalse(typeof(DBNull).IsNumericType());
    }

    [TestMethod]
    public void IsNotNumericTypeReturnsOppositeOfIsNumericType()
    {
        // Arrange
        object numericObj = 42;
        object nonNumericObj = "test";

        // Act & Assert
        Assert.IsFalse(numericObj.IsNotNumericType());
        Assert.IsTrue(nonNumericObj.IsNotNumericType());
    }
}