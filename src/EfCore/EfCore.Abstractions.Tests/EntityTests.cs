namespace EfCore.Abstractions.Tests;

// Test entities for testing purposes
public class TestEntity : Entity<int>, IConcurrencyEntity<byte[]>
{
    public TestEntity()
    {
    }

    public TestEntity(int id) : base(id)
    {
    }

    public string Name { get; set; } = string.Empty;
    public byte[]? RowVersion { get; private set; }
    public void SetRowVersion(byte[] rowVersion)
    {
        RowVersion = rowVersion;
    }
}

public class TestGuidEntity : Entity
{
    public TestGuidEntity()
    {
    }

    public TestGuidEntity(Guid id) : base(id)
    {
    }

    public string Name { get; set; } = string.Empty;
}

public class EntityTests
{
    [Fact]
    public void Entity_DefaultConstructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var entity = new TestEntity();

        // Assert
        entity.Id.ShouldBe(0); // default int value
        entity.ToString().ShouldBe("TestEntity '0'");
    }

    [Fact]
    public void Entity_ConstructorWithId_ShouldSetId()
    {
        // Arrange
        const int expectedId = 42;

        // Act
        var entity = new TestEntity(expectedId);

        // Assert
        entity.Id.ShouldBe(expectedId);
        entity.ToString().ShouldBe("TestEntity '42'");
    }

    [Fact]
    public void Entity_ToString_ShouldReturnCorrectFormat()
    {
        // Arrange
        var entity = new TestEntity(123);

        // Act
        var result = entity.ToString();

        // Assert
        result.ShouldBe("TestEntity '123'");
    }

    [Fact]
    public void Entity_IConcurrencyEntity_ShouldBeImplemented()
    {
        // Arrange & Act
        var entity = new TestEntity();

        // Assert
        entity.ShouldBeAssignableTo<IConcurrencyEntity<byte[]>>();
    }

    [Fact]
    public void Entity_IEntity_ShouldBeImplemented()
    {
        // Arrange & Act
        var entity = new TestEntity();

        // Assert
        entity.ShouldBeAssignableTo<IEntity<int>>();
    }

    [Fact]
    public void GuidEntity_InheritsFromEntity_ShouldHaveCorrectType()
    {
        // Arrange & Act
        var entity = new TestGuidEntity();

        // Assert
        entity.ShouldBeAssignableTo<Entity<Guid>>();
        entity.ShouldBeAssignableTo<IEntity<Guid>>();
    }
}