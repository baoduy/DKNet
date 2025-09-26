using DKNet.Svc.PdfGenerators.Services;

namespace Svc.PdfGenerators.Tests;

public class PropertyServiceTests
{
    // Test class with static properties
    public class TestStaticClass
    {
        public static string StringProperty { get; } = "TestValue";
        public static int IntProperty { get; } = 42;
        public static bool BoolProperty { get; } = true;
        public static string? NullProperty { get; }
    }

    [Fact]
    public void TryGetPropertyValue_WithValidStringProperty_ReturnsTrue()
    {
        // Act
        var success = PropertyService.TryGetPropertyValue<TestStaticClass, string>("StringProperty", out var value);

        // Assert
        Assert.True(success);
        Assert.Equal("TestValue", value);
    }

    [Fact]
    public void TryGetPropertyValue_WithValidIntProperty_ReturnsTrue()
    {
        // Act
        var success = PropertyService.TryGetPropertyValue<TestStaticClass, int>("IntProperty", out var value);

        // Assert
        Assert.True(success);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetPropertyValue_WithValidBoolProperty_ReturnsTrue()
    {
        // Act
        var success = PropertyService.TryGetPropertyValue<TestStaticClass, bool>("BoolProperty", out var value);

        // Assert
        Assert.True(success);
        Assert.True(value);
    }

    [Fact]
    public void TryGetPropertyValue_WithNullProperty_ReturnsTrue()
    {
        // Act
        var success = PropertyService.TryGetPropertyValue<TestStaticClass, string?>("NullProperty", out var value);

        // Assert
        Assert.True(success);
        Assert.Null(value);
    }

    [Fact]
    public void TryGetPropertyValue_WithNonExistentProperty_ReturnsFalse()
    {
        // Act
        var success =
            PropertyService.TryGetPropertyValue<TestStaticClass, string>("NonExistentProperty", out var value);

        // Assert
        Assert.False(success);
        Assert.Null(value);
    }

    [Fact]
    public void TryGetPropertyValue_WithCaseInsensitivePropertyName_ReturnsTrue()
    {
        // Act
        var success = PropertyService.TryGetPropertyValue<TestStaticClass, string>("stringproperty", out var value);

        // Assert
        Assert.True(success);
        Assert.Equal("TestValue", value);
    }

    [Fact]
    public void TryGetPropertyValue_WithEmptyPropertyName_ReturnsFalse()
    {
        // Act
        var success = PropertyService.TryGetPropertyValue<TestStaticClass, string>("", out var value);

        // Assert
        Assert.False(success);
        Assert.Null(value);
    }

    [Fact]
    public void TryGetPropertyValue_WithTypeMismatch_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<InvalidCastException>(() =>
            PropertyService.TryGetPropertyValue<TestStaticClass, int>("StringProperty", out _));
    }

    // Test with a real type from the framework
    [Fact]
    public void TryGetPropertyValue_WithFrameworkType_WorksCorrectly()
    {
        // Act - Test with DateTime.MinValue
        var success = PropertyService.TryGetPropertyValue<DateTime, DateTime>("MinValue", out var value);

        // Assert
        Assert.True(success);
        Assert.Equal(DateTime.MinValue, value);
    }

    // Test with enum type
    public enum TestEnumeration
    {
        Value1,
        Value2
    }

    public class TestEnumClass
    {
        public static TestEnumeration DefaultValue { get; } = TestEnumeration.Value1;
    }

    [Fact]
    public void TryGetPropertyValue_WithEnumProperty_ReturnsCorrectValue()
    {
        // Act
        var success =
            PropertyService.TryGetPropertyValue<TestEnumClass, TestEnumeration>("DefaultValue", out var value);

        // Assert
        Assert.True(success);
        Assert.Equal(TestEnumeration.Value1, value);
    }
}