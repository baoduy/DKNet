using DKNet.Fw.Extensions;

namespace Fw.Extensions.Tests;

public class TypeExtensionsTests
{
    #region Methods

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
    public void IsNumericTypeNullObjectReturnsFalse()
    {
        // Arrange
        object obj = null!;

        // Act
        var result = obj.IsNumericType();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsNumericTypeNullTypeReturnsFalse()
    {
        // Arrange
        Type type = null!;

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

    #endregion
}