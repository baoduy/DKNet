using DKNet.EfCore.Extensions.Extensions;

namespace EfCore.Extensions.Tests;

// Test enum for sequence testing
[SqlSequence]
public enum TestSequenceTypes
{
    [Sequence(typeof(int), StartAt = 100, IncrementsBy = 5, FormatString = "TEST-{1:000}")]
    TestSequence1,

    [Sequence(typeof(long), StartAt = 1, IncrementsBy = 1)]
    TestSequence2
}

[SqlSequence] // Uses default schema "seq"
public enum DefaultSchemaSequenceTypes
{
    [Sequence] DefaultSequence
}

public class SequenceExtensionsTests
{
    #region Methods

    [Fact]
    public void GetAttribute_WithDefaultSchema_ShouldReturnDefaultSchema()
    {
        // Act
        var attribute = SequenceExtensions.GetAttribute(typeof(DefaultSchemaSequenceTypes));

        // Assert
        attribute.ShouldNotBeNull();
        attribute.Schema.ShouldBe("seq");
    }

    [Fact]
    public void GetAttribute_WithoutAttribute_ShouldReturnNull()
    {
        // Act
        var attribute = SequenceExtensions.GetAttribute(typeof(string));

        // Assert
        attribute.ShouldBeNull();
    }

    [Fact]
    public void GetAttribute_WithValidEnum_ShouldReturnAttribute()
    {
        // Act
        var attribute = SequenceExtensions.GetAttribute(typeof(TestSequenceTypes));

        // Assert
        attribute.ShouldNotBeNull();
        attribute.Schema.ShouldBe("seq");
    }

    [Fact]
    public void GetFieldAttributeOrDefault_WithFieldAttribute_ShouldReturnAttribute()
    {
        // Act
        var attribute =
            SequenceExtensions.GetFieldAttributeOrDefault(typeof(TestSequenceTypes), TestSequenceTypes.TestSequence1);

        // Assert
        attribute.ShouldNotBeNull();
        attribute.Type.ShouldBe(typeof(int));
        attribute.StartAt.ShouldBe(100);
        attribute.IncrementsBy.ShouldBe(5);
        attribute.FormatString.ShouldBe("TEST-{1:000}");
    }

    [Fact]
    public void GetFieldAttributeOrDefault_WithoutFieldAttribute_ShouldReturnDefault()
    {
        // Act
        var attribute = SequenceExtensions.GetFieldAttributeOrDefault(
            typeof(DefaultSchemaSequenceTypes),
            DefaultSchemaSequenceTypes.DefaultSequence);

        // Assert
        attribute.ShouldNotBeNull();
        attribute.Type.ShouldBe(typeof(int)); // Default type
        attribute.StartAt.ShouldBe(-1); // Default value
        attribute.IncrementsBy.ShouldBe(-1); // Default value
    }

    [Fact]
    public void GetSequenceName_ShouldReturnFormattedName()
    {
        // Act
        var name = SequenceExtensions.GetSequenceName(TestSequenceTypes.TestSequence1);

        // Assert
        name.ShouldBe("Seq_TestSequence1");
    }

    #endregion
}