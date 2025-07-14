namespace EfCore.Abstractions.Tests;

// Test entities for testing purposes
public class TestAuditedEntity : AuditedEntity<int>
{
    public TestAuditedEntity()
    {
    }

    public TestAuditedEntity(int id) : base(id)
    {
    }

    public TestAuditedEntity(int id, string createdBy, DateTimeOffset? createdOn = null) : base(id)
    {
        SetCreatedBy(createdBy, createdOn);
    }

    public string Name { get; set; } = string.Empty;
}

public class TestAuditedGuidEntity : AuditedEntity
{
    public TestAuditedGuidEntity()
    {
    }

    public TestAuditedGuidEntity(Guid id) : base(id)
    {
    }

    public TestAuditedGuidEntity(Guid id, string createdBy, DateTimeOffset? createdOn = null) : base(id)
    {
        SetCreatedBy(createdBy, createdOn);
    }

    public string Name { get; set; } = string.Empty;
}

public class AuditedEntityTests
{
    [Fact]
    public void AuditedEntity_DefaultConstructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var entity = new TestAuditedEntity();

        // Assert
        entity.Id.ShouldBe(0);
        entity.CreatedBy.ShouldBeNull();
        entity.CreatedOn.ShouldBe(default);
        entity.UpdatedBy.ShouldBeNull();
        entity.UpdatedOn.ShouldBeNull();
    }

    [Fact]
    public void AuditedEntity_ConstructorWithId_ShouldSetId()
    {
        // Arrange
        const int expectedId = 42;

        // Act
        var entity = new TestAuditedEntity(expectedId);

        // Assert
        entity.Id.ShouldBe(expectedId);
        entity.CreatedBy.ShouldBeNull();
    }

    [Fact]
    public void AuditedEntity_ConstructorWithIdAndCreatedBy_ShouldSetBoth()
    {
        // Arrange
        const int expectedId = 42;
        const string expectedCreatedBy = "testuser";
        var expectedCreatedOn = DateTimeOffset.UtcNow.AddMinutes(-5);

        // Act
        var entity = new TestAuditedEntity(expectedId, expectedCreatedBy, expectedCreatedOn);

        // Assert
        entity.Id.ShouldBe(expectedId);
        entity.CreatedBy.ShouldBe(expectedCreatedBy);
        entity.CreatedOn.ShouldBe(expectedCreatedOn);
    }

    [Fact]
    public void SetCreatedBy_WithUserName_ShouldSetCreatedByAndCreatedOn()
    {
        // Arrange
        var entity = new TestAuditedEntity();
        const string userName = "testuser";
        var beforeCall = DateTimeOffset.UtcNow;

        // Act
        entity.SetCreatedBy(userName);
        var afterCall = DateTimeOffset.UtcNow;

        // Assert
        entity.CreatedBy.ShouldBe(userName);
        entity.CreatedOn.ShouldBeGreaterThanOrEqualTo(beforeCall);
        entity.CreatedOn.ShouldBeLessThanOrEqualTo(afterCall);
    }

    [Fact]
    public void SetCreatedBy_WithUserNameAndTimestamp_ShouldSetBoth()
    {
        // Arrange
        var entity = new TestAuditedEntity();
        const string userName = "testuser";
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-10);

        // Act
        entity.SetCreatedBy(userName, timestamp);

        // Assert
        entity.CreatedBy.ShouldBe(userName);
        entity.CreatedOn.ShouldBe(timestamp);
    }

    [Fact]
    public void SetCreatedBy_WhenAlreadySet_ShouldNotChange()
    {
        // Arrange
        var entity = new TestAuditedEntity();
        const string originalUser = "originaluser";
        const string newUser = "newuser";
        var originalTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10);

        entity.SetCreatedBy(originalUser, originalTimestamp);

        // Act
        entity.SetCreatedBy(newUser);

        // Assert
        entity.CreatedBy.ShouldBe(originalUser);
        entity.CreatedOn.ShouldBe(originalTimestamp);
    }

    [Fact]
    public void SetCreatedBy_WithNullUserName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var entity = new TestAuditedEntity();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => entity.SetCreatedBy(null!));
    }

    [Fact]
    public void SetUpdatedBy_WithUserName_ShouldSetUpdatedByAndUpdatedOn()
    {
        // Arrange
        var entity = new TestAuditedEntity();
        const string userName = "testuser";
        var beforeCall = DateTimeOffset.UtcNow;

        // Act
        entity.SetUpdatedBy(userName);
        var afterCall = DateTimeOffset.UtcNow;

        // Assert
        entity.UpdatedBy.ShouldBe(userName);
        entity.UpdatedOn.ShouldNotBeNull();
        entity.UpdatedOn.Value.ShouldBeGreaterThanOrEqualTo(beforeCall);
        entity.UpdatedOn.Value.ShouldBeLessThanOrEqualTo(afterCall);
    }

    [Fact]
    public void SetUpdatedBy_WithUserNameAndTimestamp_ShouldSetBoth()
    {
        // Arrange
        var entity = new TestAuditedEntity();
        const string userName = "testuser";
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-5);

        // Act
        entity.SetUpdatedBy(userName, timestamp);

        // Assert
        entity.UpdatedBy.ShouldBe(userName);
        entity.UpdatedOn.ShouldBe(timestamp);
    }

    [Fact]
    public void SetUpdatedBy_WithNullOrEmptyUserName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var entity = new TestAuditedEntity();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => entity.SetUpdatedBy(null!));
        Should.Throw<ArgumentNullException>(() => entity.SetUpdatedBy(string.Empty));
    }

    [Fact]
    public void LastModifiedBy_WhenNotUpdated_ShouldReturnCreatedBy()
    {
        // Arrange
        var entity = new TestAuditedEntity();
        const string createdBy = "creator";
        entity.SetCreatedBy(createdBy);

        // Act & Assert
        entity.LastModifiedBy.ShouldBe(createdBy);
    }

    [Fact]
    public void LastModifiedBy_WhenUpdated_ShouldReturnUpdatedBy()
    {
        // Arrange
        var entity = new TestAuditedEntity();
        const string createdBy = "creator";
        const string updatedBy = "updater";

        entity.SetCreatedBy(createdBy);
        entity.SetUpdatedBy(updatedBy);

        // Act & Assert
        entity.LastModifiedBy.ShouldBe(updatedBy);
    }

    [Fact]
    public void LastModifiedOn_WhenNotUpdated_ShouldReturnCreatedOn()
    {
        // Arrange
        var entity = new TestAuditedEntity();
        var createdOn = DateTimeOffset.UtcNow.AddMinutes(-10);
        entity.SetCreatedBy("creator", createdOn);

        // Act & Assert
        entity.LastModifiedOn.ShouldBe(createdOn);
    }

    [Fact]
    public void LastModifiedOn_WhenUpdated_ShouldReturnUpdatedOn()
    {
        // Arrange
        var entity = new TestAuditedEntity();
        var createdOn = DateTimeOffset.UtcNow.AddMinutes(-10);
        var updatedOn = DateTimeOffset.UtcNow.AddMinutes(-5);

        entity.SetCreatedBy("creator", createdOn);
        entity.SetUpdatedBy("updater", updatedOn);

        // Act & Assert
        entity.LastModifiedOn.ShouldBe(updatedOn);
    }

    [Fact]
    public void AuditedEntity_ShouldImplementRequiredInterfaces()
    {
        // Arrange & Act
        var entity = new TestAuditedEntity();

        // Assert
        entity.ShouldBeAssignableTo<IAuditedEntity<int>>();
        entity.ShouldBeAssignableTo<IAuditedProperties>();
        entity.ShouldBeAssignableTo<IEntity<int>>();
        entity.ShouldBeAssignableTo<IConcurrencyEntity>();
    }

    [Fact]
    public void AuditedGuidEntity_DefaultConstructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var entity = new TestAuditedGuidEntity();

        // Assert
        entity.Id.ShouldBe(Guid.Empty);
        entity.CreatedBy.ShouldBeNull();
    }

    [Fact]
    public void AuditedGuidEntity_ConstructorWithId_ShouldSetId()
    {
        // Arrange
        var expectedId = Guid.NewGuid();

        // Act
        var entity = new TestAuditedGuidEntity(expectedId);

        // Assert
        entity.Id.ShouldBe(expectedId);
    }

    [Fact]
    public void AuditedGuidEntity_ConstructorWithIdAndCreatedBy_ShouldSetBoth()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        const string expectedCreatedBy = "testuser";
        var expectedCreatedOn = DateTimeOffset.UtcNow.AddMinutes(-5);

        // Act
        var entity = new TestAuditedGuidEntity(expectedId, expectedCreatedBy, expectedCreatedOn);

        // Assert
        entity.Id.ShouldBe(expectedId);
        entity.CreatedBy.ShouldBe(expectedCreatedBy);
        entity.CreatedOn.ShouldBe(expectedCreatedOn);
    }

    [Fact]
    public void AuditedGuidEntity_ShouldInheritFromCorrectBaseClass()
    {
        // Arrange & Act
        var entity = new TestAuditedGuidEntity();

        // Assert
        entity.ShouldBeAssignableTo<AuditedEntity<Guid>>();
        entity.ShouldBeAssignableTo<IAuditedEntity<Guid>>();
    }
}