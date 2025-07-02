using DKNet.Fw.Extensions;

namespace Fw.Extensions.Tests;


public class TypeExtensionsTests
{
    [Fact]
    public void IsImplementOfSameTypeReturnsFalse()
    {
        // Arrange
        var type = typeof(string);

        // Act
        var result = type.IsImplementOf(type);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsImplementOfDerivedTypeReturnsTrue()
    {
        // Arrange
        var derivedType = typeof(List<int>);
        var baseType = typeof(IList<int>);

        // Act
        var result = derivedType.IsImplementOf(baseType);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsImplementOfInterfaceTypeReturnsTrue()
    {
        // Arrange
        var type = typeof(List<int>);
        var interfaceType = typeof(IEnumerable<int>);

        // Act
        var result = type.IsImplementOf(interfaceType);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsImplementOfNonMatchingTypeReturnsFalse()
    {
        // Arrange
        var type = typeof(string);
        var nonMatchingType = typeof(int);

        // Act
        var result = type.IsImplementOf(nonMatchingType);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsImplementOfNullTypeReturnsFalse()
    {
        // Arrange
        Type type = null;
        var matchingType = typeof(int);

        // Act
        var result = type.IsImplementOf(matchingType);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsImplementOfNullMatchingTypeReturnsFalse()
    {
        // Arrange
        var type = typeof(string);
        Type matchingType = null;

        // Act
        var result = type.IsImplementOf(matchingType);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsImplementOfGenericTypeReturnsTrue()
    {
        // Arrange
        var type = typeof(List<int>);
        var genericType = typeof(IList<>);

        // Act
        var result = type.IsImplementOf(genericType);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsNotNumericTypeNonNumericTypeReturnsTrue()
    {
        // Arrange
        object obj = "test";

        // Act
        var result = obj.IsNotNumericType();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsNotNumericTypeNumericTypeReturnsFalse()
    {
        // Arrange
        object obj = 123;

        // Act
        var result = obj.IsNotNumericType();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsNumericTypeNumericTypeReturnsTrue()
    {
        // Arrange
        var type = typeof(int);

        // Act
        var result = type.IsNumericType();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsNumericTypeNonNumericTypeReturnsFalse()
    {
        // Arrange
        var type = typeof(string);

        // Act
        var result = type.IsNumericType();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsNumericTypeNullTypeReturnsFalse()
    {
        // Arrange
        Type type = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => type.IsNumericType());
    }

    [Fact]
    public void IsNumericTypeNumericObjectReturnsTrue()
    {
        // Arrange
        object obj = 123.45;

        // Act
        var result = obj.IsNumericType();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsNumericTypeNonNumericObjectReturnsFalse()
    {
        // Arrange
        object obj = "test";

        // Act
        var result = obj.IsNumericType();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsNumericTypeNullObjectReturnsFalse()
    {
        // Arrange
        object obj = null;

        // Act
        var result = obj.IsNumericType();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsImplementOfGenericTypeDefinitionReturnsTrue()
    {
        // Arrange
        var type = typeof(List<int>);
        var genericType = typeof(IList<>);

        // Act
        var result = type.IsImplementOf(genericType);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsImplementOfDerivedGenericTypeReturnsTrue()
    {
        // Arrange
        var type = typeof(List<int>);
        var baseType = typeof(IEnumerable<>);

        // Act
        var result = type.IsImplementOf(baseType);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsImplementOfGenericMethodReturnsCorrectResult()
    {
        // Arrange
        var type = typeof(List<int>);

        // Act
        var result = type.IsImplementOf<IEnumerable<int>>();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsImplementOfNonGenericBaseClassReturnsTrue()
    {
        // Arrange
        var type = typeof(ArgumentException);
        var baseType = typeof(Exception);

        // Act
        var result = type.IsImplementOf(baseType);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsImplementOfGenericBaseClassReturnsTrue()
    {
        // Arrange
        var type = typeof(List<int>);
        var baseType = typeof(ICollection<>);

        // Act
        var result = type.IsImplementOf(baseType);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsNumericTypeAllNumericTypesReturnTrue()
    {
        // Arrange & Act & Assert
        typeof(byte).IsNumericType().ShouldBeTrue();
        typeof(sbyte).IsNumericType().ShouldBeTrue();
        typeof(ushort).IsNumericType().ShouldBeTrue();
        typeof(short).IsNumericType().ShouldBeTrue();
        typeof(uint).IsNumericType().ShouldBeTrue();
        typeof(int).IsNumericType().ShouldBeTrue();
        typeof(ulong).IsNumericType().ShouldBeTrue();
        typeof(long).IsNumericType().ShouldBeTrue();
        typeof(float).IsNumericType().ShouldBeTrue();
        typeof(double).IsNumericType().ShouldBeTrue();
        typeof(decimal).IsNumericType().ShouldBeTrue();
    }

    [Fact]
    public void IsNumericTypeAllNonNumericTypesReturnFalse()
    {
        // Arrange & Act & Assert
        typeof(string).IsNumericType().ShouldBeFalse();
        typeof(bool).IsNumericType().ShouldBeFalse();
        typeof(char).IsNumericType().ShouldBeFalse();
        typeof(DateTime).IsNumericType().ShouldBeFalse();
        typeof(object).IsNumericType().ShouldBeFalse();
        typeof(DBNull).IsNumericType().ShouldBeFalse();
    }

    [Fact]
    public void IsNotNumericTypeReturnsOppositeOfIsNumericType()
    {
        // Arrange
        object numericObj = 42;
        object nonNumericObj = "test";

        // Act & Assert
        numericObj.IsNotNumericType().ShouldBeFalse();
        nonNumericObj.IsNotNumericType().ShouldBeTrue();
    }

    [Fact]
    public void IsImplementOfShouldHandleInheritanceChain()
    {
        // Arrange
        var derivedType = typeof(ArgumentNullException);
        var baseType = typeof(SystemException);
        
        // Act
        var result = derivedType.IsImplementOf(baseType);
        
        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsImplementOfShouldHandleMultipleInterfaceImplementation()
    {
        // Arrange
        var type = typeof(List<string>);
        var interface1 = typeof(ICollection<string>);
        var interface2 = typeof(IEnumerable<string>);
        
        // Act & Assert
        type.IsImplementOf(interface1).ShouldBeTrue();
        type.IsImplementOf(interface2).ShouldBeTrue();
    }

    [Fact]
    public void IsImplementOfShouldReturnFalseForUnrelatedTypes()
    {
        // Arrange
        var type1 = typeof(string);
        var type2 = typeof(int);
        var type3 = typeof(DateTime);
        
        // Act & Assert
        type1.IsImplementOf(type2).ShouldBeFalse();
        type2.IsImplementOf(type3).ShouldBeFalse();
        type3.IsImplementOf(type1).ShouldBeFalse();
    }

    [Fact]
    public void IsImplementOfGenericShouldWorkWithOpenGenericTypes()
    {
        // Arrange
        var closedGenericType = typeof(Dictionary<string, int>);
        var openGenericInterface = typeof(IDictionary<,>);
        
        // Act
        var result = closedGenericType.IsImplementOf(openGenericInterface);
        
        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsNumericTypeShouldHandleNullableNumericTypes()
    {
        // Arrange & Act & Assert
        typeof(int?).IsNumericType().ShouldBeTrue();
        typeof(double?).IsNumericType().ShouldBeTrue();
        typeof(decimal?).IsNumericType().ShouldBeTrue();
        typeof(float?).IsNumericType().ShouldBeTrue();
        typeof(long?).IsNumericType().ShouldBeTrue();
    }

    [Fact]
    public void IsNumericTypeShouldReturnFalseForNullableNonNumericTypes()
    {
        // Arrange & Act & Assert
        typeof(bool?).IsNumericType().ShouldBeFalse();
        typeof(char?).IsNumericType().ShouldBeFalse();
        typeof(DateTime?).IsNumericType().ShouldBeFalse();
    }

    [Fact]
    public void IsImplementOfShouldWorkWithMultiLevelInheritance()
    {
        // Arrange
        var grandChildType = typeof(ArgumentOutOfRangeException);
        var grandParentType = typeof(Exception);
        
        // Act
        var result = grandChildType.IsImplementOf(grandParentType);
        
        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsImplementOfShouldHandleGenericInterfaceWithConstraints()
    {
        // Arrange  
        var type = typeof(SortedList<int, string>);
        var genericInterface = typeof(IDictionary<,>);
        
        // Act
        var result = type.IsImplementOf(genericInterface);
        
        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
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
        byteObj.IsNumericType().ShouldBeTrue();
        sbyteObj.IsNumericType().ShouldBeTrue();
        shortObj.IsNumericType().ShouldBeTrue();
        ushortObj.IsNumericType().ShouldBeTrue();
        intObj.IsNumericType().ShouldBeTrue();
        uintObj.IsNumericType().ShouldBeTrue();
        longObj.IsNumericType().ShouldBeTrue();
        ulongObj.IsNumericType().ShouldBeTrue();
        floatObj.IsNumericType().ShouldBeTrue();
        doubleObj.IsNumericType().ShouldBeTrue();
        decimalObj.IsNumericType().ShouldBeTrue();
    }

    [Fact]
    public void IsImplementOfWithGenericMethodShouldReturnCorrectResultForComplexTypes()
    {
        // Arrange
        var type = typeof(SortedDictionary<string, int>);
        
        // Act & Assert
        type.IsImplementOf<IDictionary<string, int>>().ShouldBeTrue();
        type.IsImplementOf<ICollection<KeyValuePair<string, int>>>().ShouldBeTrue();
        type.IsImplementOf<IEnumerable<KeyValuePair<string, int>>>().ShouldBeTrue();
        type.IsImplementOf<IList<string>>().ShouldBeFalse();
    }
}