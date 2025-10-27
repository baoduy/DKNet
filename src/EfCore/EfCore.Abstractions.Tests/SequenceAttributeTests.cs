namespace EfCore.Abstractions.Tests;

public class SequenceAttributeTests
{
    #region Methods

    [Fact]
    public void SequenceAttribute_AllSupportedTypes_ShouldBeAccepted()
    {
        // Arrange & Act & Assert
        Should.NotThrow(() => new SequenceAttribute(typeof(byte)));
        Should.NotThrow(() => new SequenceAttribute(typeof(short)));
        Should.NotThrow(() => new SequenceAttribute(typeof(int)));
        Should.NotThrow(() => new SequenceAttribute(typeof(long)));
    }

    [Fact]
    public void SequenceAttribute_AttributeUsage_ShouldBeField()
    {
        // Arrange
        var attributeType = typeof(SequenceAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), true)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        attributeUsage.ShouldNotBeNull();
        attributeUsage.ValidOn.ShouldBe(AttributeTargets.Field);
    }

    [Fact]
    public void SequenceAttribute_DefaultConstructor_ShouldUseIntType()
    {
        // Act
        var attribute = new SequenceAttribute();

        // Assert
        attribute.Type.ShouldBe(typeof(int));
        attribute.Cyclic.ShouldBeTrue();
        attribute.IncrementsBy.ShouldBe(-1);
        attribute.Max.ShouldBe(-1);
        attribute.Min.ShouldBe(-1);
        attribute.StartAt.ShouldBe(-1);
        attribute.FormatString.ShouldBeNull();
    }

    [Fact]
    public void SequenceAttribute_DefaultValues_ShouldMatchSpecification()
    {
        // Act
        var attribute = new SequenceAttribute();

        // Assert
        attribute.Cyclic.ShouldBeTrue();
        attribute.IncrementsBy.ShouldBe(-1);
        attribute.Max.ShouldBe(-1);
        attribute.Min.ShouldBe(-1);
        attribute.StartAt.ShouldBe(-1);
    }

    [Fact]
    public void SequenceAttribute_FormatString_ShouldAcceptNullValue()
    {
        // Arrange
        var attribute = new SequenceAttribute();

        // Act
        attribute.FormatString = null;

        // Assert
        attribute.FormatString.ShouldBeNull();
    }

    [Fact]
    public void SequenceAttribute_IsSealed_ShouldBeTrue()
    {
        // Arrange
        var attributeType = typeof(SequenceAttribute);

        // Act & Assert
        attributeType.IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void SequenceAttribute_Properties_ShouldBeSettable()
    {
        // Arrange
        var attribute = new SequenceAttribute();

        // Act
        attribute.Cyclic = false;
        attribute.IncrementsBy = 5;
        attribute.Max = 1000;
        attribute.Min = 1;
        attribute.StartAt = 10;
        attribute.FormatString = "SEQ-{1:0000}";

        // Assert
        attribute.Cyclic.ShouldBeFalse();
        attribute.IncrementsBy.ShouldBe(5);
        attribute.Max.ShouldBe(1000);
        attribute.Min.ShouldBe(1);
        attribute.StartAt.ShouldBe(10);
        attribute.FormatString.ShouldBe("SEQ-{1:0000}");
    }

    [Fact]
    public void SequenceAttribute_WithByteType_ShouldSetCorrectType()
    {
        // Act
        var attribute = new SequenceAttribute(typeof(byte));

        // Assert
        attribute.Type.ShouldBe(typeof(byte));
    }

    [Fact]
    public void SequenceAttribute_WithIntType_ShouldSetCorrectType()
    {
        // Act
        var attribute = new SequenceAttribute(typeof(int));

        // Assert
        attribute.Type.ShouldBe(typeof(int));
    }

    [Fact]
    public void SequenceAttribute_WithLongType_ShouldSetCorrectType()
    {
        // Act
        var attribute = new SequenceAttribute(typeof(long));

        // Assert
        attribute.Type.ShouldBe(typeof(long));
    }

    [Fact]
    public void SequenceAttribute_WithShortType_ShouldSetCorrectType()
    {
        // Act
        var attribute = new SequenceAttribute(typeof(short));

        // Assert
        attribute.Type.ShouldBe(typeof(short));
    }

    [Fact]
    public void SequenceAttribute_WithUnsupportedType_ShouldThrowNotSupportedException()
    {
        // Act & Assert
        Should.Throw<NotSupportedException>(() => new SequenceAttribute(typeof(string)));
        Should.Throw<NotSupportedException>(() => new SequenceAttribute(typeof(decimal)));
        Should.Throw<NotSupportedException>(() => new SequenceAttribute(typeof(float)));
        Should.Throw<NotSupportedException>(() => new SequenceAttribute(typeof(double)));
        Should.Throw<NotSupportedException>(() => new SequenceAttribute(typeof(DateTime)));
    }

    #endregion
}