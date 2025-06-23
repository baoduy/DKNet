using DKNet.Fw.Extensions;
using Fw.Extensions.Tests.TestObjects;

namespace Fw.Extensions.Tests;

[TestClass]
public class PropertyExtensionsTests
{
    [TestMethod]
    public void GetPropertyShouldReturnNullForNullObject()
    {
        // Arrange
        var propertyName = "Name";

        // Act
        var property = ((TestItem3)null).GetProperty(propertyName);

        // Assert
        Assert.IsNull(property);
    }

    [TestMethod]
    public void GetPropertyShouldReturnNullForNullOrEmptyPropertyName()
    {
        // Arrange
        using var item = new TestItem3("Duy");

        // Act
        var property1 = item.GetProperty(null);
        var property2 = item.GetProperty("");

        // Assert
        Assert.IsNull(property1);
        Assert.IsNull(property2);
    }

    [TestMethod]
    public void GetPropertyShouldReturnPublicProperty()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "Name";

        // Act
        var property = item.GetProperty(propertyName);

        // Assert
        Assert.IsNotNull(property);
        Assert.AreEqual("Name", property.Name);
    }

    [TestMethod]
    public void GetPropertyShouldReturnPrivateProperty()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "PrivateObj";

        // Act
        var property = item.GetProperty(propertyName);

        // Assert
        Assert.IsNotNull(property);
        Assert.AreEqual("PrivateObj", property.Name);
    }

    [TestMethod]
    public void GetPropertyShouldReturnProtectedProperty()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "ProtectedObj";

        // Act
        var property = item.GetProperty(propertyName);

        // Assert
        Assert.IsNotNull(property);
        Assert.AreEqual("ProtectedObj", property.Name);
    }

    [TestMethod]
    public void PropertyValueShouldReturnNullForNullObject()
    {
        // Arrange
        var propertyName = "Name";

        // Act
        var value = ((TestItem3)null).GetPropertyValue(propertyName);

        // Assert
        Assert.IsNull(value);
    }

    [TestMethod]
    public void PropertyValueShouldReturnNullForNullOrEmptyPropertyName()
    {
        // Arrange
        using var item = new TestItem3("Duy");

        // Act
        var value1 = item.GetPropertyValue(null);
        var value2 = item.GetPropertyValue("");

        // Assert
        Assert.IsNull(value1);
        Assert.IsNull(value2);
    }

    [TestMethod]
    public void PropertyValueShouldReturnPublicPropertyValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "Name";

        // Act
        var value = item.GetPropertyValue(propertyName);

        // Assert
        Assert.AreEqual("Duy", value);
    }

    [TestMethod]
    public void PropertyValueShouldReturnPrivatePropertyValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "PrivateObj";

        // Act
        var value = item.GetPropertyValue(propertyName);

        // Assert
        Assert.IsNotNull(value);
    }

    [TestMethod]
    public void PropertyValueShouldReturnProtectedPropertyValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "ProtectedObj";

        // Act
        var value = item.GetPropertyValue(propertyName);

        // Assert
        Assert.IsNotNull(value);
    }

    [TestMethod]
    public void PropertyValueShouldReturnNestedPropertyValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");

        // Act
        var value = item.GetPropertyValue(nameof(TestItem3.Name));

        // Assert
        Assert.AreEqual("Duy", value);
    }

    [TestMethod]
    public void SetPropertyValueShouldThrowForNullObject()
    {
        // Arrange
        var propertyName = "Name";
        var value = "Duy";

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() =>
            ((TestItem3)null).SetPropertyValue(propertyName, value));
    }

    [TestMethod]
    public void SetPropertyValueShouldThrowForNullOrEmptyPropertyName()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var value = "Duy";

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => item.SetPropertyValue((string)null, value));
        Assert.ThrowsException<ArgumentNullException>(() => item.SetPropertyValue("", value));
    }

    [TestMethod]
    public void SetPropertyValueShouldSetPublicPropertyValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "Name";
        var value = "NewName";

        // Act
        item.SetPropertyValue(propertyName, value);

        // Assert
        Assert.AreEqual("NewName", item.Name);
    }

    [TestMethod]
    public void SetPropertyValueShouldSetPrivatePropertyValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "PrivateObj";
        var value = "NewPrivateValue";

        // Act
        item.SetPropertyValue(propertyName, value);

        // Assert
        Assert.AreEqual("NewPrivateValue", item.GetPropertyValue(propertyName));
    }

    [TestMethod]
    public void SetPropertyValueShouldSetProtectedPropertyValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "ProtectedObj";
        var value = "NewProtectedValue";

        // Act
        item.SetPropertyValue(propertyName, value);

        // Assert
        Assert.AreEqual("NewProtectedValue", item.GetPropertyValue(propertyName));
    }

    [TestMethod]
    public void SetPropertyValueShouldSetNestedPropertyValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var value = "NewNestedName";

        // Act
        item.SetPropertyValue(nameof(TestItem3.Name), value);

        // Assert
        Assert.AreEqual("NewNestedName", item.GetPropertyValue(nameof(TestItem3.Name)));
    }

    [TestMethod]
    public void SetPropertyValueShouldHandleNullValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var propertyName = "Name";

        // Act
        item.SetPropertyValue(propertyName, null);

        // Assert
        Assert.IsNull(item.Name);
    }

    [TestMethod]
    public void SetPropertyValueShouldHandleEnumValue()
    {
        // Arrange
        using var item = new TestItem3("Duy");

        // Act
        item.SetPropertyValue(nameof(TestItem3.Type), TestEnumObject.Enum2);

        // Assert
        Assert.AreEqual(TestEnumObject.Enum2, item.Type);
    }

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public void GetPropertyValueShouldReturnNullForNonExistentNestedProperty()
    {
        // Arrange
        using var item = new TestItem3("Duy");

        // Act
        var value = item.GetPropertyValue("NonExistent.Property");

        // Assert
        Assert.IsNull(value);
    }

    [TestMethod]
    public void GetPropertyValueShouldHandleNullIntermediateNestedProperty()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        item.SetPropertyValue("Name", null); // Set Name to null

        // Act
        var value = item.GetPropertyValue("Name.Length"); // This should return null because Name is null

        // Assert
        Assert.IsNull(value);
    }

    [TestMethod]
    public void GetPropertyWithTypeObjectShouldReturnProperty()
    {
        // Arrange
        var type = typeof(TestItem3);

        // Act
        var property = type.GetProperty("Name");

        // Assert
        Assert.IsNotNull(property);
        Assert.AreEqual("Name", property.Name);
    }

    [TestMethod]
    public void SetPropertyValueWithPropertyInfoShouldThrowForNullObject()
    {
        // Arrange
        var property = typeof(TestItem3).GetProperty("Name");
        var value = "Test";

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() =>
            ((TestItem3)null).SetPropertyValue(property, value));
    }

    [TestMethod]
    public void SetPropertyValueWithPropertyInfoShouldThrowForNullProperty()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var value = "Test";

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() =>
            item.SetPropertyValue((PropertyInfo)null, value));
    }

    [TestMethod]
    public void TrySetPropertyValueWithPropertyInfoShouldThrowForNullObject()
    {
        // Arrange
        var property = typeof(TestItem3).GetProperty("Name");
        var value = "Test";

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() =>
            ((TestItem3)null).TrySetPropertyValue(property, value));
    }

    [TestMethod]
    public void TrySetPropertyValueWithPropertyInfoShouldThrowForNullProperty()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var value = "Test";

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() =>
            item.TrySetPropertyValue((PropertyInfo)null, value));
    }

    [TestMethod]
    public void TrySetPropertyValueWithStringNameShouldThrowForNullObject()
    {
        // Arrange
        var propertyName = "Name";
        var value = "Test";

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() =>
            ((TestItem3)null).TrySetPropertyValue(propertyName, value));
    }

    [TestMethod]
    public void TrySetPropertyValueWithStringNameShouldThrowForNullPropertyName()
    {
        // Arrange
        using var item = new TestItem3("Duy");
        var value = "Test";

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() =>
            item.TrySetPropertyValue((string)null, value));
        Assert.ThrowsException<ArgumentNullException>(() =>
            item.TrySetPropertyValue("", value));
    }
}