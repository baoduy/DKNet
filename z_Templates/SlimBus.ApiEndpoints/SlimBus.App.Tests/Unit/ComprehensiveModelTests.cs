using SlimBus.AppServices.Profiles.V1.Actions;
using SlimBus.AppServices.Profiles.V1.Queries;
using SlimBus.AppServices.Share;

namespace SlimBus.App.Tests.Unit;

public class ComprehensiveModelTests
{
    [Fact]
    public void ProfileResult_RequiredProperties_MustBeProvided()
    {
        // Arrange & Act - Records with required properties will allow creation with required values
        var result = new ProfileResult
        {
            Id = Guid.NewGuid(),
            Name = "Valid Name",
            Email = "test@example.com"
        };

        // Assert
        result.Name.ShouldNotBeNull();
        result.Email.ShouldNotBeNull();
    }

    [Fact]
    public void ProfileResult_AllPropertiesSet_ShouldWork()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Complete User";
        var email = "complete@example.com";
        var phone = "+1234567890";

        // Act
        var result = new ProfileResult
        {
            Id = id,
            Name = name,
            Email = email,
            Phone = phone
        };

        // Assert
        result.Id.ShouldBe(id);
        result.Name.ShouldBe(name);
        result.Email.ShouldBe(email);
        result.Phone.ShouldBe(phone);
    }

    [Fact]
    public void CreateProfileCommand_DefaultConstructor_ShouldInitializeProperties()
    {
        // Act
        var command = new CreateProfileCommand();

        // Assert - Properties are initialized with default values but may be null initially
        // This tests that the object can be created successfully
        command.ShouldNotBeNull();
    }

    [Fact]
    public void CreateProfileCommand_SetAllProperties_ShouldWork()
    {
        // Arrange
        var email = "create@example.com";
        var phone = "+1234567890";
        var name = "Create User";

        // Act
        var command = new CreateProfileCommand
        {
            Email = email,
            Phone = phone,
            Name = name
        };

        // Assert
        command.Email.ShouldBe(email);
        command.Phone.ShouldBe(phone);
        command.Name.ShouldBe(name);
    }

    [Theory]
    [InlineData("test1@example.com")]
    [InlineData("test.user@domain.co.uk")]
    [InlineData("user+tag@example.org")]
    public void CreateProfileCommand_WithVariousEmails_ShouldWork(string email)
    {
        // Act
        var command = new CreateProfileCommand
        {
            Email = email,
            Phone = "+1234567890",
            Name = "Test User"
        };

        // Assert
        command.Email.ShouldBe(email);
    }

    [Theory]
    [InlineData("+1234567890")]
    [InlineData("+44 20 1234 5678")]
    [InlineData("+65 9123 4567")]
    public void CreateProfileCommand_WithVariousPhones_ShouldWork(string phone)
    {
        // Act
        var command = new CreateProfileCommand
        {
            Email = "test@example.com",
            Phone = phone,
            Name = "Test User"
        };

        // Assert
        command.Phone.ShouldBe(phone);
    }

    [Fact]
    public void UpdateProfileCommand_AllOptionalProperties_CanBeNull()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var command = new UpdateProfileCommand
        {
            Id = id,
            Email = null,
            Phone = null,
            Name = null
        };

        // Assert
        command.Id.ShouldBe(id);
        command.Email.ShouldBeNull();
        command.Phone.ShouldBeNull();
        command.Name.ShouldBeNull();
    }

    [Fact]
    public void UpdateProfileCommand_PartialUpdate_ShouldWork()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var command = new UpdateProfileCommand
        {
            Id = id,
            Email = "updated@example.com",
            Phone = null, // Keep existing phone
            Name = null  // Keep existing name
        };

        // Assert
        command.Id.ShouldBe(id);
        command.Email.ShouldBe("updated@example.com");
        command.Phone.ShouldBeNull();
        command.Name.ShouldBeNull();
    }

    [Fact]
    public void DeleteProfileCommand_WithValidId_ShouldWork()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var command = new DeleteProfileCommand { Id = id };

        // Assert
        command.Id.ShouldBe(id);
    }

    [Fact]
    public void DeleteProfileCommand_WithEmptyGuid_ShouldStillWork()
    {
        // Act
        var command = new DeleteProfileCommand { Id = Guid.Empty };

        // Assert
        command.Id.ShouldBe(Guid.Empty);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 10)]
    [InlineData(10, 100)]
    [InlineData(50, 1000)]
    public void PageableQuery_WithValidValues_ShouldWork(int pageIndex, int pageSize)
    {
        // Act
        var query = new PageableQuery
        {
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        // Assert
        query.PageIndex.ShouldBe(pageIndex);
        query.PageSize.ShouldBe(pageSize);
    }

    [Fact]
    public void PageableQuery_SetToNegativeValues_ShouldStillSet()
    {
        // Act
        var query = new PageableQuery
        {
            PageIndex = -1,
            PageSize = -10
        };

        // Assert - The class itself doesn't validate, validation happens elsewhere
        query.PageIndex.ShouldBe(-1);
        query.PageSize.ShouldBe(-10);
    }

    [Fact]
    public void PageableQuery_SetToLargeValues_ShouldWork()
    {
        // Act
        var query = new PageableQuery
        {
            PageIndex = int.MaxValue,
            PageSize = int.MaxValue
        };

        // Assert
        query.PageIndex.ShouldBe(int.MaxValue);
        query.PageSize.ShouldBe(int.MaxValue);
    }

    [Fact]
    public void ProfileQuery_WithValidId_ShouldWork()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var query = new ProfileQuery { Id = id };

        // Assert
        query.Id.ShouldBe(id);
    }

    [Fact]
    public void ProfileQuery_WithEmptyGuid_ShouldWork()
    {
        // Act
        var query = new ProfileQuery { Id = Guid.Empty };

        // Assert
        query.Id.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void PageProfilePageQuery_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var query = new PageProfilePageQuery();

        // Assert
        query.PageSize.ShouldBe(100);
        query.PageIndex.ShouldBe(0);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 50)]
    [InlineData(10, 200)]
    [InlineData(1000, 1000)]
    public void PageProfilePageQuery_WithValidValues_ShouldWork(int pageIndex, int pageSize)
    {
        // Act
        var query = new PageProfilePageQuery
        {
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        // Assert
        query.PageIndex.ShouldBe(pageIndex);
        query.PageSize.ShouldBe(pageSize);
    }
}