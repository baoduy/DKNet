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

    [Fact]
    public void TryConvertToEnum_WithIntType_ShouldThrowArgumentException()
    {
        // Arrange
        var intType = typeof(int);
        var value = 1;

        // Act & Assert
        Should.Throw<ArgumentException>(() => intType.TryConvertToEnum(value, out _));
    }

    [Fact]
    public void TryConvertToEnum_WithInvalidStringValue_ShouldReturnFalse()
    {
        // Arrange
        var enumType = typeof(HbdTypes);
        var value = "invalid";

        // Act
        var result = enumType.TryConvertToEnum(value, out var enumValue);

        // Assert
        result.ShouldBeFalse();
        enumValue.ShouldBeNull();
    }

    [Fact]
    public void TryConvertToEnum_WithNegativeValue_ShouldHandleAppropriately()
    {
        // Arrange
        var enumType = typeof(HbdTypes);
        var value = -1;

        // Act
        var result = enumType.TryConvertToEnum(value, out var enumValue);

        // Assert
        // This should either succeed with the underlying value or fail depending on enum definition
        // Since HbdTypes doesn't have -1, it might still convert but to an undefined value
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
    }

    [Fact]
    public void TryConvertToEnum_WithNonEnumType_ShouldThrowArgumentException()
    {
        // Arrange
        var nonEnumType = typeof(string);
        var value = "test";

        // Act & Assert
        Should.Throw<ArgumentException>(() => nonEnumType.TryConvertToEnum(value, out _))
            .Message.ShouldContain("not an enum type");
    }

    [Fact]
    public void TryConvertToEnum_WithNullableEnumType_ShouldReturnTrue()
    {
        // Arrange
        var enumType = typeof(HbdTypes?);
        var value = 2;

        // Act
        var result = enumType.TryConvertToEnum(value, out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.NamedEnum);
    }

    [Fact]
    public void TryConvertToEnum_WithOverflowValue_ShouldReturnFalse()
    {
        // Arrange
        var enumType = typeof(HbdTypes);
        var value = long.MaxValue;

        // Act
        var result = enumType.TryConvertToEnum(value, out var enumValue);

        // Assert
        result.ShouldBeFalse();
        enumValue.ShouldBeNull();
    }

    [Fact]
    public void TryConvertToEnum_WithShortValue_ShouldReturnTrue()
    {
        // Arrange
        var enumType = typeof(HbdTypes);
        short value = 3;

        // Act
        var result = enumType.TryConvertToEnum(value, out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.Enum);
    }

    [Fact]
    public void TryConvertToEnum_WithUIntValue_ShouldReturnTrue()
    {
        // Arrange
        var enumType = typeof(HbdTypes);
        uint value = 1;

        // Act
        var result = enumType.TryConvertToEnum(value, out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.DescriptionEnum);
    }

    [Fact]
    public void TryConvertToEnum_WithValidByteValue_ShouldReturnTrue()
    {
        // Arrange
        var enumType = typeof(HbdTypes);
        byte value = 1;

        // Act
        var result = enumType.TryConvertToEnum(value, out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.DescriptionEnum);
    }

    [Fact]
    public void TryConvertToEnum_WithValidDecimalValue_ShouldConvertToInt()
    {
        // Arrange
        var enumType = typeof(HbdTypes);
        var value = 2.0m;

        // Act
        var result = enumType.TryConvertToEnum(value, out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.NamedEnum);
    }

    [Fact]
    public void TryConvertToEnum_WithValidDoubleValue_ShouldConvertToInt()
    {
        // Arrange
        var enumType = typeof(HbdTypes);
        var value = 1.0;

        // Act
        var result = enumType.TryConvertToEnum(value, out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.DescriptionEnum);
    }

    [Fact]
    public void TryConvertToEnum_WithValidIntValue_ShouldReturnTrue()
    {
        // Arrange
        var enumType = typeof(HbdTypes);
        var value = 2;

        // Act
        var result = enumType.TryConvertToEnum(value, out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.NamedEnum);
    }

    [Fact]
    public void TryConvertToEnum_WithValidLongValue_ShouldReturnTrue()
    {
        // Arrange
        var enumType = typeof(HbdTypes);
        var value = 3L;

        // Act
        var result = enumType.TryConvertToEnum(value, out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.Enum);
    }

    [Fact]
    public void TryConvertToEnum_WithValidStringValue_ShouldReturnTrue()
    {
        // Arrange
        var enumType = typeof(HbdTypes);
        var value = "1";

        // Act
        var result = enumType.TryConvertToEnum(value, out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.DescriptionEnum);
    }

    [Fact]
    public void TryConvertToEnum_WithZeroValue_ShouldReturnTrue()
    {
        // Arrange
        var enumType = typeof(HbdTypes);
        var value = 0;

        // Act
        var result = enumType.TryConvertToEnum(value, out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.None);
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithByteValue_ShouldReturnTrue()
    {
        // Arrange
        object value = (byte)3;

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.Enum);
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithDecimalValue_ShouldReturnTrue()
    {
        // Arrange
        object value = 1.0m;

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.DescriptionEnum);
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithDoubleValue_ShouldReturnTrue()
    {
        // Arrange
        object value = 3.0;

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.Enum);
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithEnumValue_ShouldReturnTrue()
    {
        // Arrange
        object value = HbdTypes.NamedEnum;

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.NamedEnum);
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithFloatValue_ShouldReturnTrue()
    {
        // Arrange
        object value = 2.0f;

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.NamedEnum);
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithInvalidStringValue_ShouldReturnFalse()
    {
        // Arrange
        object value = "invalid";

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeFalse();
        enumValue.ShouldBeNull();
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithLongValue_ShouldReturnTrue()
    {
        // Arrange
        object value = 1L;

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.DescriptionEnum);
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithNegativeValue_ShouldHandleAppropriately()
    {
        // Arrange
        object value = -1;

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        // -1 will be converted even though it's not a defined enum value
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithOverflowValue_ShouldReturnFalse()
    {
        // Arrange
        object value = long.MaxValue;

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeFalse();
        enumValue.ShouldBeNull();
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithSByteValue_ShouldReturnTrue()
    {
        // Arrange
        object value = (sbyte)1;

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.DescriptionEnum);
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithShortValue_ShouldReturnTrue()
    {
        // Arrange
        object value = (short)2;

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.NamedEnum);
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithUIntValue_ShouldReturnTrue()
    {
        // Arrange
        object value = 3u;

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.Enum);
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithULongValue_ShouldReturnTrue()
    {
        // Arrange
        object value = 2ul;

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.NamedEnum);
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithUShortValue_ShouldReturnTrue()
    {
        // Arrange
        object value = (ushort)3;

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.Enum);
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithValidIntValue_ShouldReturnTrue()
    {
        // Arrange
        object value = 2;

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.NamedEnum);
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithValidStringValue_ShouldReturnTrue()
    {
        // Arrange
        object value = "1";

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.DescriptionEnum);
    }

    [Fact]
    public void TryConvertToEnumGeneric_WithZeroValue_ShouldReturnTrue()
    {
        // Arrange
        object value = 0;

        // Act
        var result = value.TryConvertToEnum<HbdTypes>(out var enumValue);

        // Assert
        result.ShouldBeTrue();
        enumValue.ShouldNotBeNull();
        enumValue.ShouldBe(HbdTypes.None);
    }

    #endregion
}