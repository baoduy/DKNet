using SlimBus.AppServices.Profiles.V1.Actions;
using SlimBus.AppServices.Profiles.V1.Queries;

namespace SlimBus.App.Tests.Unit;

public class ProfileModelsTests
{
    #region Methods

    [Fact]
    public void CreateProfileCommandPropertiesShouldSetCorrectly()
    {
        // Arrange
        var command = new CreateProfileCommand();
        var email = "test@example.com";
        var phone = "+1234567890";
        var name = "Test User";

        // Act
        command.Email = email;
        command.Phone = phone;
        command.Name = name;

        // Assert
        command.Email.ShouldBe(email);
        command.Phone.ShouldBe(phone);
        command.Name.ShouldBe(name);
    }

    [Theory]
    [InlineData("test@example.com", "+1234567890", "Test User")]
    [InlineData("another@example.com", "+0987654321", "Another User")]
    public void CreateProfileCommandWithDifferentValuesShouldWork(string email, string phone, string name)
    {
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

    [Fact]
    public void DeleteProfileCommandPropertiesShouldSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var command = new DeleteProfileCommand
        {
            Id = id
        };

        // Assert
        command.Id.ShouldBe(id);
    }

    [Fact]
    public void DeleteProfileCommandWithGuidEmptyShouldWork()
    {
        // Act
        var command = new DeleteProfileCommand
        {
            Id = Guid.Empty
        };

        // Assert
        command.Id.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void ProfileResultPhoneCanBeNull()
    {
        // Arrange & Act
        var result = new ProfileResult
        {
            Id = Guid.NewGuid(),
            Name = "Jane Doe",
            Email = "jane.doe@example.com",
            Phone = null
        };

        // Assert
        result.Phone.ShouldBeNull();
    }

    [Fact]
    public void ProfileResultPropertiesShouldSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "John Doe";
        var email = "john.doe@example.com";
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
    public void UpdateProfileCommandOptionalPropertiesCanBeNull()
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
    public void UpdateProfileCommandPropertiesShouldSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = "updated@example.com";
        var phone = "+0987654321";
        var name = "Updated User";

        // Act
        var command = new UpdateProfileCommand
        {
            Id = id,
            Email = email,
            Phone = phone,
            Name = name
        };

        // Assert
        command.Id.ShouldBe(id);
        command.Email.ShouldBe(email);
        command.Phone.ShouldBe(phone);
        command.Name.ShouldBe(name);
    }

    [Theory]
    [InlineData("updated1@example.com", "+1111111111", "Updated User 1")]
    [InlineData("updated2@example.com", "+2222222222", "Updated User 2")]
    public void UpdateProfileCommandWithDifferentValuesShouldWork(string email, string phone, string name)
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var command = new UpdateProfileCommand
        {
            Id = id,
            Email = email,
            Phone = phone,
            Name = name
        };

        // Assert
        command.Id.ShouldBe(id);
        command.Email.ShouldBe(email);
        command.Phone.ShouldBe(phone);
        command.Name.ShouldBe(name);
    }

    #endregion
}