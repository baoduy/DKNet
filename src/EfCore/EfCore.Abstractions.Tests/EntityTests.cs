namespace EfCore.Abstractions.Tests;

// Test entities for testing purposes
public class TestEntity : Entity<int>
{
    public TestEntity()
    {
    }

    public TestEntity(int id) : base(id)
    {
    }

    public string Name { get; set; } = string.Empty;
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
        entity.RowVersion.ShouldBeNull();
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
        entity.RowVersion.ShouldBeNull();
        entity.ToString().ShouldBe("TestEntity '42'");
    }

    [Fact]
    public void Entity_SetRowVersion_ShouldUpdateRowVersion()
    {
        // Arrange
        var entity = new TestEntity();
        var rowVersion = new byte[] { 1, 2, 3, 4 };

        // Act
        entity.SetRowVersion(rowVersion);

        // Assert
        entity.RowVersion.ShouldBe(rowVersion);
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
    public void GuidEntity_DefaultConstructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var entity = new TestGuidEntity();

        // Assert
        entity.Id.ShouldBe(Guid.Empty);
        entity.RowVersion.ShouldBeNull();
    }

    [Fact]
    public void GuidEntity_ConstructorWithId_ShouldSetId()
    {
        // Arrange
        var expectedId = Guid.NewGuid();

        // Act
        var entity = new TestGuidEntity(expectedId);

        // Assert
        entity.Id.ShouldBe(expectedId);
        entity.RowVersion.ShouldBeNull();
    }

    [Fact]
    public void Entity_IConcurrencyEntity_ShouldBeImplemented()
    {
        // Arrange & Act
        var entity = new TestEntity();

        // Assert
        entity.ShouldBeAssignableTo<IConcurrencyEntity>();
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