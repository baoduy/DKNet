namespace EfCore.Extensions.Tests;

// Test enum for sequence testing
[SqlSequence("test_seq")]
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
    [Sequence]
    DefaultSequence
}


public class SequenceRegisterTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public void GetAttribute_WithValidEnum_ShouldReturnAttribute()
    {
        // Act
        var attribute = SequenceRegister.GetAttribute(typeof(TestSequenceTypes));

        // Assert
        attribute.ShouldNotBeNull();
        attribute.Schema.ShouldBe("test_seq");
    }

    [Fact]
    public void GetAttribute_WithDefaultSchema_ShouldReturnDefaultSchema()
    {
        // Act
        var attribute = SequenceRegister.GetAttribute(typeof(DefaultSchemaSequenceTypes));

        // Assert
        attribute.ShouldNotBeNull();
        attribute.Schema.ShouldBe("seq");
    }

    [Fact]
    public void GetAttribute_WithoutAttribute_ShouldReturnNull()
    {
        // Act
        var attribute = SequenceRegister.GetAttribute(typeof(string));

        // Assert
        attribute.ShouldBeNull();
    }

    [Fact]
    public void GetFieldAttributeOrDefault_WithFieldAttribute_ShouldReturnAttribute()
    {
        // Act
        var attribute = SequenceRegister.GetFieldAttributeOrDefault(typeof(TestSequenceTypes), TestSequenceTypes.TestSequence1);

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
        var attribute = SequenceRegister.GetFieldAttributeOrDefault(typeof(DefaultSchemaSequenceTypes), DefaultSchemaSequenceTypes.DefaultSequence);

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
        var name = SequenceRegister.GetSequenceName(TestSequenceTypes.TestSequence1);

        // Assert
        name.ShouldBe("Sequence_TestSequence1");
    }

    [Fact]
    public async Task NextSeqValue_WithValidSequence_ShouldReturnValue()
    {
        // Arrange
        
        var options = new DbContextOptionsBuilder()
            .UseSqlServer(fixture.GetConnectionString("SequenceDb"))
            .UseAutoConfigModel(op => op.ScanFrom(typeof(TestSequenceTypes).Assembly))
            .Options;

        await using var context = new DbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Act
        var value = await context.NextSeqValue(TestSequenceTypes.TestSequence1);

        // Assert
        value.ShouldNotBeNull();
        value.ShouldBeOfType<int>();
        ((int)value).ShouldBeGreaterThanOrEqualTo(100); // Should start from the StartAt value
    }

    [Fact]
    public async Task NextSeqValueWithFormat_ShouldReturnFormattedValue()
    {
        // Arrange

        var options = new DbContextOptionsBuilder()
            .UseSqlServer(fixture.GetConnectionString("SequenceDb"))
            .UseAutoConfigModel(op => op.ScanFrom(typeof(TestSequenceTypes).Assembly))
            .Options;

        await using var context = new DbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Act
        var formattedValue = await context.NextSeqValueWithFormat(TestSequenceTypes.TestSequence1);

        // Assert
        formattedValue.ShouldNotBeNullOrEmpty();
        formattedValue.ShouldStartWith("TEST-");
    }
}