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
    [ExpectedException(typeof(ArgumentNullException))]
    public void IsNumericTypeNullTypeReturnsFalse()
    {
        // Arrange
        Type type = null;

        // Act
        type.IsNumericType();
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

    [TestMethod]
    public void IsImplementOfShouldHandleInheritanceChain()
    {
        // Arrange
        var derivedType = typeof(ArgumentNullException);
        var baseType = typeof(SystemException);
        
        // Act
        var result = derivedType.IsImplementOf(baseType);
        
        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsImplementOfShouldHandleMultipleInterfaceImplementation()
    {
        // Arrange
        var type = typeof(List<string>);
        var interface1 = typeof(ICollection<string>);
        var interface2 = typeof(IEnumerable<string>);
        
        // Act & Assert
        Assert.IsTrue(type.IsImplementOf(interface1));
        Assert.IsTrue(type.IsImplementOf(interface2));
    }

    [TestMethod]
    public void IsImplementOfShouldReturnFalseForUnrelatedTypes()
    {
        // Arrange
        var type1 = typeof(string);
        var type2 = typeof(int);
        var type3 = typeof(DateTime);
        
        // Act & Assert
        Assert.IsFalse(type1.IsImplementOf(type2));
        Assert.IsFalse(type2.IsImplementOf(type3));
        Assert.IsFalse(type3.IsImplementOf(type1));
    }

    [TestMethod]
    public void IsImplementOfGenericShouldWorkWithOpenGenericTypes()
    {
        // Arrange
        var closedGenericType = typeof(Dictionary<string, int>);
        var openGenericInterface = typeof(IDictionary<,>);
        
        // Act
        var result = closedGenericType.IsImplementOf(openGenericInterface);
        
        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsNumericTypeShouldHandleNullableNumericTypes()
    {
        // Arrange & Act & Assert
        Assert.IsTrue(typeof(int?).IsNumericType());
        Assert.IsTrue(typeof(double?).IsNumericType());
        Assert.IsTrue(typeof(decimal?).IsNumericType());
        Assert.IsTrue(typeof(float?).IsNumericType());
        Assert.IsTrue(typeof(long?).IsNumericType());
    }

    [TestMethod]
    public void IsNumericTypeShouldReturnFalseForNullableNonNumericTypes()
    {
        // Arrange & Act & Assert
        Assert.IsFalse(typeof(bool?).IsNumericType());
        Assert.IsFalse(typeof(char?).IsNumericType());
        Assert.IsFalse(typeof(DateTime?).IsNumericType());
    }

    [TestMethod]
    public void IsImplementOfShouldWorkWithMultiLevelInheritance()
    {
        // Arrange
        var grandChildType = typeof(ArgumentOutOfRangeException);
        var grandParentType = typeof(Exception);
        
        // Act
        var result = grandChildType.IsImplementOf(grandParentType);
        
        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsImplementOfShouldHandleGenericInterfaceWithConstraints()
    {
        // Arrange  
        var type = typeof(SortedList<int, string>);
        var genericInterface = typeof(IDictionary<,>);
        
        // Act
        var result = type.IsImplementOf(genericInterface);
        
        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsNumericTypeObjectShouldHandleBoxedPrimitives()
    {
        // Arrange
        object byteObj = (byte)1;
        object sbyteObj = (sbyte)1;
        object shortObj = (short)1;
        object ushortObj = (ushort)1;
        object intObj = 1;
        object uintObj = 1u;
        object longObj = 1L;
        object ulongObj = 1ul;
        object floatObj = 1.0f;
        object doubleObj = 1.0;
        object decimalObj = 1.0m;
        
        // Act & Assert
        Assert.IsTrue(byteObj.IsNumericType());
        Assert.IsTrue(sbyteObj.IsNumericType());
        Assert.IsTrue(shortObj.IsNumericType());
        Assert.IsTrue(ushortObj.IsNumericType());
        Assert.IsTrue(intObj.IsNumericType());
        Assert.IsTrue(uintObj.IsNumericType());
        Assert.IsTrue(longObj.IsNumericType());
        Assert.IsTrue(ulongObj.IsNumericType());
        Assert.IsTrue(floatObj.IsNumericType());
        Assert.IsTrue(doubleObj.IsNumericType());
        Assert.IsTrue(decimalObj.IsNumericType());
    }

    [TestMethod]
    public void IsImplementOfWithGenericMethodShouldReturnCorrectResultForComplexTypes()
    {
        // Arrange
        var type = typeof(SortedDictionary<string, int>);
        
        // Act & Assert
        Assert.IsTrue(type.IsImplementOf<IDictionary<string, int>>());
        Assert.IsTrue(type.IsImplementOf<ICollection<KeyValuePair<string, int>>>());
        Assert.IsTrue(type.IsImplementOf<IEnumerable<KeyValuePair<string, int>>>());
        Assert.IsFalse(type.IsImplementOf<IList<string>>());
    }
}