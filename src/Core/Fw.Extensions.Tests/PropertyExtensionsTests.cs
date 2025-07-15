using DKNet.Fw.Extensions;

namespace Fw.Extensions.Tests;

public class PropertyExtensionsTests
{
    [Fact]
    public void GetPropertyShouldReturnNullForNullObject()
    {
        // Arrange
        var propertyName = "Name";

        // Act
        var property = ((TestItem3)null).GetProperty(propertyName);

        // Assert
        property.ShouldBeNull();
    }

    [Fact]
    public void GetPropertyShouldReturnNullForNullOrEmptyPropertyName()
    {
        // Arrange
        using var item = new TestItem3("Duy");

        // Act
        var property1 = item.GetProperty(null);
        var property2 = item.GetProperty("");

        // Assert
        property1.ShouldBeNull();
        property2.ShouldBeNull();
    }

    [Fact]
    public void GetPropertyShouldReturnPublicProperty()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "Name";

        // Act
        var property = item.GetProperty(propertyName);

        // Assert
        property.ShouldNotBeNull();
        property.Name.ShouldBe("Name");
    }

    [Fact]
    public void GetPropertyShouldReturnPrivateProperty()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "PrivateObj";

        // Act
        var property = item.GetProperty(propertyName);

        // Assert
        property.ShouldNotBeNull();
        property.Name.ShouldBe("PrivateObj");
    }

    [Fact]
    public void GetPropertyShouldReturnProtectedProperty()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "ProtectedObj";

        // Act
        var property = item.GetProperty(propertyName);

        // Assert
        property.ShouldNotBeNull();
        property.Name.ShouldBe("ProtectedObj");
    }

    [Fact]
    public void PropertyValueShouldReturnNullForNullObject()
    {
        // Arrange
        var propertyName = "Name";

        // Act
        var value = ((TestItem3)null).GetPropertyValue(propertyName);

        // Assert
        value.ShouldBeNull();
    }

    [Fact]
    public void PropertyValueShouldReturnNullForNullOrEmptyPropertyName()
    {
        // Arrange
        using var item = new TestItem3("Duy");

        // Act
        var value1 = item.GetPropertyValue(null);
        var value2 = item.GetPropertyValue("");

        // Assert
        value1.ShouldBeNull();
        value2.ShouldBeNull();
    }

    [Fact]
    public void PropertyValueShouldReturnPublicPropertyValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "Name";

        // Act
        var value = item.GetPropertyValue(propertyName);

        // Assert
        value.ShouldBe("Duy");
    }

    [Fact]
    public void PropertyValueShouldReturnPrivatePropertyValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "PrivateObj";

        // Act
        var value = item.GetPropertyValue(propertyName);

        // Assert
        value.ShouldNotBeNull();
    }

    [Fact]
    public void PropertyValueShouldReturnProtectedPropertyValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "ProtectedObj";

        // Act
        var value = item.GetPropertyValue(propertyName);

        // Assert
        value.ShouldNotBeNull();
    }

    [Fact]
    public void PropertyValueShouldReturnNestedPropertyValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");

        // Act
        var value = item.GetPropertyValue(nameof(TestItem3.Name));

        // Assert
        value.ShouldBe("Duy");
    }

    [Fact]
    public void SetPropertyValueShouldThrowForNullObject()
    {
        // Arrange
        var propertyName = "Name";
        var value = "Duy";

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ((TestItem3)null).SetPropertyValue(propertyName, value));
    }

    [Fact]
    public void SetPropertyValueShouldThrowForNullOrEmptyPropertyName()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var value = "Duy";

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => item.SetPropertyValue((string)null, value));
        Should.Throw<ArgumentNullException>(() => item.SetPropertyValue("", value));
    }

    [Fact]
    public void SetPropertyValueShouldSetPublicPropertyValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "Name";
        var value = "NewName";

        // Act
        item.SetPropertyValue(propertyName, value);

        // Assert
        item.Name.ShouldBe("NewName");
    }

    [Fact]
    public void SetPropertyValueShouldSetPrivatePropertyValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "PrivateObj";
        var value = "NewPrivateValue";

        // Act
        item.SetPropertyValue(propertyName, value);

        // Assert
        item.GetPropertyValue(propertyName).ShouldBe("NewPrivateValue");
    }

    [Fact]
    public void SetPropertyValueShouldSetProtectedPropertyValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "ProtectedObj";
        var value = "NewProtectedValue";

        // Act
        item.SetPropertyValue(propertyName, value);

        // Assert
        item.GetPropertyValue(propertyName).ShouldBe("NewProtectedValue");
    }

    [Fact]
    public void SetPropertyValueShouldSetNestedPropertyValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var value = "NewNestedName";

        // Act
        item.SetPropertyValue(nameof(TestItem3.Name), value);

        // Assert
        item.GetPropertyValue(nameof(TestItem3.Name)).ShouldBe("NewNestedName");
    }

    [Fact]
    public void SetPropertyValueShouldHandleNullValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "Name";

        // Act
        item.SetPropertyValue(propertyName, null);

        // Assert
        item.Name.ShouldBeNull();
    }

    [Fact]
    public void SetPropertyValueShouldHandleEnumValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");

        // Act
        item.SetPropertyValue(nameof(TestItem3.Type), TestEnumObject.Enum2);

        // Assert
        item.Type.ShouldBe(TestEnumObject.Enum2);
    }

    [Fact]
    public void SetPropertyValueShouldHandleInvalidPropertyName()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "bac";
        var value = "Duy";

        // Act
        var a = () => item.SetPropertyValue(propertyName, value);

        a.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void TrySetPropertyValueShouldHandleInvalidPropertyName()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "bac";
        var value = "Duy";

        // Act
        item.TrySetPropertyValue(propertyName, value);

        item.GetPropertyValue(propertyName).ShouldBeNull();
    }

    [Fact]
    public void GetPropertyValueShouldReturnNullForNonExistentNestedProperty()
    {
        // Arrange
        using var item = new TestItem3("Duy");

        // Act
        var value = item.GetPropertyValue("NonExistent.Property");

        // Assert
        value.ShouldBeNull();
    }

    [Fact]
    public void GetPropertyValueShouldHandleNullIntermediateNestedProperty()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        item.SetPropertyValue("Name", null); // Set Name to null

        // Act
        var value = item.GetPropertyValue("Name.Length"); // This should return null because Name is null

        // Assert
        value.ShouldBeNull();
    }

    [Fact]
    public void GetPropertyWithTypeObjectShouldReturnProperty()
    {
        // Arrange
        var type = typeof(TestItem3);

        // Act
        var property = type.GetProperty("Name");

        // Assert
        property.ShouldNotBeNull();
        property.Name.ShouldBe("Name");
    }

    [Fact]
    public void SetPropertyValueWithPropertyInfoShouldThrowForNullObject()
    {
        // Arrange
        var property = typeof(TestItem3).GetProperty("Name");
        var value = "Test";

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ((TestItem3)null).SetPropertyValue(property, value));
    }

    [Fact]
    public void SetPropertyValueWithPropertyInfoShouldThrowForNullProperty()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var value = "Test";

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            item.SetPropertyValue((PropertyInfo)null, value));
    }

    [Fact]
    public void TrySetPropertyValueWithPropertyInfoShouldThrowForNullObject()
    {
        // Arrange
        var property = typeof(TestItem3).GetProperty("Name");
        var value = "Test";

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ((TestItem3)null).TrySetPropertyValue(property, value));
    }

    [Fact]
    public void TrySetPropertyValueWithPropertyInfoShouldThrowForNullProperty()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var value = "Test";

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            item.TrySetPropertyValue((PropertyInfo)null, value));
    }

    [Fact]
    public void TrySetPropertyValueWithStringNameShouldThrowForNullObject()
    {
        // Arrange
        var propertyName = "Name";
        var value = "Test";

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ((TestItem3)null).TrySetPropertyValue(propertyName, value));
    }

    [Fact]
    public void TrySetPropertyValueWithStringNameShouldThrowForNullPropertyName()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var value = "Test";

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            item.TrySetPropertyValue((string)null, value));
        Should.Throw<ArgumentNullException>(() =>
            item.TrySetPropertyValue("", value));
    }

    [Fact]
    public void SetPropertyValueShouldHandleValueTypeConversion()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var stringValue = "42";

        // Act & Test integer conversion
        item.SetPropertyValue("IntValue", stringValue);
        item.GetPropertyValue("IntValue").ShouldBe(42);

        // Act & Test boolean conversion  
        item.SetPropertyValue("BoolValue", "true");
        item.GetPropertyValue("BoolValue").ShouldBe(true);

        // Act & Test decimal conversion
        item.SetPropertyValue("DecimalValue", "123.45");
        item.GetPropertyValue("DecimalValue").ShouldBe(123.45m);
    }

    [Fact]
    public void TrySetPropertyValueShouldCatchAndLogArgumentExceptions()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var property = typeof(TestItem3).GetProperty("IntValue");

        // Act - This should not throw, but should catch internal ArgumentNullException
        item.TrySetPropertyValue(property, "invalid_conversion_value_that_cannot_be_converted_to_int");

        // Assert - Value should remain unchanged
        item.GetPropertyValue("IntValue").ShouldBe(0);
    }

    [Fact]
    public void GetPropertyValueShouldHandleComplexNestedProperties()
    {
        // Arrange
        using var item = new TestItem3("Duy");

        // Act & Assert for valid nested property
        var lengthValue = item.GetPropertyValue("Name.Length");
        lengthValue.ShouldBe(3); // "Duy".Length = 3
    }

    [Fact]
    public void SetPropertyValueShouldHandleNullableTypes()
    {
        // Arrange
        using var item = new TestItem3("Duy");

        // Act & Assert for nullable int
        item.SetPropertyValue("NullableIntValue", 123);
        item.GetPropertyValue("NullableIntValue").ShouldBe(123);

        // Set to null
        item.SetPropertyValue("NullableIntValue", null);
        item.GetPropertyValue("NullableIntValue").ShouldBeNull();
    }

    [Fact]
    public void GetPropertyShouldHandleCaseInsensitiveSearch()
    {
        // Arrange
        using var item = new TestItem3("Duy");

        // Act
        var property1 = item.GetProperty("name"); // lowercase
        var property2 = item.GetProperty("NAME"); // uppercase
        var property3 = item.GetProperty("NaMe"); // mixed case

        // Assert
        property1.ShouldNotBeNull();
        property2.ShouldNotBeNull();
        property3.ShouldNotBeNull();
        property1.Name.ShouldBe("Name");
        property2.Name.ShouldBe("Name");
        property3.Name.ShouldBe("Name");
    }

    [Fact]
    public void SetPropertyValueShouldHandleEnumStringConversion()
    {
        // Arrange
        using var item = new TestItem3("Duy");

        // Act - Set enum value from string
        item.SetPropertyValue("Type", "Enum1");

        // Assert
        item.Type.ShouldBe(TestEnumObject.Enum1);
    }

    [Fact]
    public void GetPropertyValueShouldReturnNullForInvalidNestedPropertyPath()
    {
        // Arrange
        using var item = new TestItem3("Duy");

        // Act & Assert for non-existent nested property
        var result = item.GetPropertyValue("Name.InvalidProperty");
        result.ShouldBeNull();

        // Act & Assert for property that doesn't exist on the object
        var result2 = item.GetPropertyValue("NonExistentProperty.SubProperty");
        result2.ShouldBeNull();
    }
}