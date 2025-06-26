using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Extensions.Registers;

namespace EfCore.Extensions.Tests;

// Test enum for sequence testing
[SqlSequence("test_seq")]
public enum TestSequenceEnum
{
    [Sequence(typeof(int), StartAt = 100, IncrementsBy = 5, FormatString = "TEST-{1:000}")]
    TestSequence1,
    
    [Sequence(typeof(long), StartAt = 1, IncrementsBy = 1)]
    TestSequence2
}

[SqlSequence] // Uses default schema "seq"
public enum DefaultSchemaSequenceEnum
{
    [Sequence]
    DefaultSequence
}

[TestClass]
public class SequenceRegisterTests : SqlServerTestBase
{
    [TestMethod]
    public void GetAttribute_WithValidEnum_ShouldReturnAttribute()
    {
        // Act
        var attribute = SequenceRegister.GetAttribute(typeof(TestSequenceEnum));

        // Assert
        attribute.ShouldNotBeNull();
        attribute.Schema.ShouldBe("test_seq");
    }

    [TestMethod]
    public void GetAttribute_WithDefaultSchema_ShouldReturnDefaultSchema()
    {
        // Act
        var attribute = SequenceRegister.GetAttribute(typeof(DefaultSchemaSequenceEnum));

        // Assert
        attribute.ShouldNotBeNull();
        attribute.Schema.ShouldBe("seq");
    }

    [TestMethod]
    public void GetAttribute_WithoutAttribute_ShouldReturnNull()
    {
        // Act
        var attribute = SequenceRegister.GetAttribute(typeof(string));

        // Assert
        attribute.ShouldBeNull();
    }

    [TestMethod]
    public void GetFieldAttributeOrDefault_WithFieldAttribute_ShouldReturnAttribute()
    {
        // Act
        var attribute = SequenceRegister.GetFieldAttributeOrDefault(typeof(TestSequenceEnum), TestSequenceEnum.TestSequence1);

        // Assert
        attribute.ShouldNotBeNull();
        attribute.Type.ShouldBe(typeof(int));
        attribute.StartAt.ShouldBe(100);
        attribute.IncrementsBy.ShouldBe(5);
        attribute.FormatString.ShouldBe("TEST-{1:000}");
    }

    [TestMethod]
    public void GetFieldAttributeOrDefault_WithoutFieldAttribute_ShouldReturnDefault()
    {
        // Act
        var attribute = SequenceRegister.GetFieldAttributeOrDefault(typeof(DefaultSchemaSequenceEnum), DefaultSchemaSequenceEnum.DefaultSequence);

        // Assert
        attribute.ShouldNotBeNull();
        attribute.Type.ShouldBe(typeof(int)); // Default type
        attribute.StartAt.ShouldBe(-1); // Default value
        attribute.IncrementsBy.ShouldBe(-1); // Default value
    }

    [TestMethod]
    public void GetSequenceName_ShouldReturnFormattedName()
    {
        // Act
        var name = SequenceRegister.GetSequenceName(TestSequenceEnum.TestSequence1);

        // Assert
        name.ShouldBe("Sequence_TestSequence1");
    }

    [TestMethod]
    public async Task NextSeqValue_WithValidSequence_ShouldReturnValue()
    {
        // Arrange
        var container = await StartSqlContainerAsync();
        var connectionString = container.GetConnectionString();
        
        var options = new DbContextOptionsBuilder()
            .UseSqlServer(connectionString)
            .UseAutoConfigModel(op => op.ScanFrom(typeof(TestSequenceEnum).Assembly))
            .Options;

        await using var context = new DbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Act
        var value = await context.NextSeqValue(TestSequenceEnum.TestSequence1);

        // Assert
        value.ShouldNotBeNull();
        value.ShouldBeOfType<int>();
        ((int)value).ShouldBeGreaterThanOrEqualTo(100); // Should start from the StartAt value
    }

    [TestMethod]
    public async Task NextSeqValueWithFormat_ShouldReturnFormattedValue()
    {
        // Arrange
        var container = await StartSqlContainerAsync();
        var connectionString = container.GetConnectionString();
        
        var options = new DbContextOptionsBuilder()
            .UseSqlServer(connectionString)
            .UseAutoConfigModel(op => op.ScanFrom(typeof(TestSequenceEnum).Assembly))
            .Options;

        await using var context = new DbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Act
        var formattedValue = await context.NextSeqValueWithFormat(TestSequenceEnum.TestSequence1);

        // Assert
        formattedValue.ShouldNotBeNullOrEmpty();
        formattedValue.ShouldStartWith("TEST-");
    }
}