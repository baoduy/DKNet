using SlimBus.Domains.Features.Profiles.Entities;

namespace SlimBus.App.Tests.Unit;

public class ExtendedDomainEntityTests
{
    [Fact]
    public void CustomerProfileConstructorWithFullParametersShouldSetAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "John Doe";
        var membershipNo = "MEM12345";
        var email = "john.doe@example.com";
        var phone = "+1234567890";
        var userId = "user123";

        // Act
        var profile = new CustomerProfile(id, name, membershipNo, email, phone, userId);

        // Assert
        profile.Id.ShouldBe(id);
        profile.Name.ShouldBe(name);
        profile.MembershipNo.ShouldBe(membershipNo);
        profile.Email.ShouldBe(email);
        profile.Phone.ShouldBe(phone);
    }

    [Fact]
    public void CustomerProfileUpdateWithAllParametersShouldUpdateAllFields()
    {
        // Arrange
        var profile = new CustomerProfile("Original Name", "MEM123", "original@example.com", "+1111111111", "user1");
        var avatar = "avatar.jpg";
        var newName = "Updated Name";
        var newPhone = "+2222222222";
        var birthday = new DateTime(1990, 1, 1);
        var userId = "user2";

        // Act
        profile.Update(avatar, newName, newPhone, birthday, userId);

        // Assert
        profile.Avatar.ShouldBe(avatar);
        profile.Name.ShouldBe(newName);
        profile.Phone.ShouldBe(newPhone);
        profile.BirthDay.ShouldBe(birthday);
    }

    [Fact]
    public void CustomerProfileUpdateWithNullValuesShouldHandleNullsCorrectly()
    {
        // Arrange
        var originalName = "Original Name";
        var originalPhone = "+1111111111";
        var profile = new CustomerProfile(originalName, "MEM123", "original@example.com", originalPhone, "user1");

        // Act
        profile.Update(avatar: null, null, null, null, "user2");

        // Assert
        profile.Avatar.ShouldBeNull();
        profile.Name.ShouldBe(originalName); // Should not change
        profile.Phone.ShouldBe(originalPhone); // Should not change
        profile.BirthDay.ShouldBeNull();
    }

    [Fact]
    public void CustomerProfileUpdateWithEmptyStringsShouldNotUpdateNameAndPhone()
    {
        // Arrange
        var originalName = "Original Name";
        var originalPhone = "+1111111111";
        var profile = new CustomerProfile(originalName, "MEM123", "original@example.com", originalPhone, "user1");

        // Act
        profile.Update("avatar.jpg", "", "", new DateTime(1990, 1, 1), "user2");

        // Assert
        profile.Avatar.ShouldBe("avatar.jpg");
        profile.Name.ShouldBe(originalName); // Should not change with empty string
        profile.Phone.ShouldBe(originalPhone); // Should not change with empty string
        profile.BirthDay.ShouldBe(new DateTime(1990, 1, 1));
    }

    [Fact]
    public void CustomerProfileUpdateWithWhitespaceStringsShouldUpdateNameAndPhone()
    {
        // Arrange
        var originalName = "Original Name";
        var originalPhone = "+1111111111";
        var profile = new CustomerProfile(originalName, "MEM123", "original@example.com", originalPhone, "user1");

        // Act - string.IsNullOrEmpty() returns false for whitespace strings, so they will update
        profile.Update("avatar.jpg", "   ", "   ", new DateTime(1990, 1, 1), "user2");

        // Assert
        profile.Avatar.ShouldBe("avatar.jpg");
        profile.Name.ShouldBe("   "); // Will be updated since IsNullOrEmpty("   ") is false
        profile.Phone.ShouldBe("   "); // Will be updated since IsNullOrEmpty("   ") is false
        profile.BirthDay.ShouldBe(new DateTime(1990, 1, 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void CustomerProfileUpdateWithVariousEmptyStringsShouldNotUpdateName(string? emptyString)
    {
        // Arrange
        var originalName = "Original Name";
        var profile = new CustomerProfile(originalName, "MEM123", "original@example.com", "+1111111111", "user1");

        // Act
        profile.Update("avatar.jpg", emptyString, "+2222222222", birthday: null, "user2");

        // Assert
        profile.Name.ShouldBe(originalName);
        profile.Phone.ShouldBe("+2222222222"); // Phone should update
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void CustomerProfileUpdateWithVariousEmptyStringsShouldNotUpdatePhone(string? emptyString)
    {
        // Arrange
        var originalPhone = "+1111111111";
        var profile = new CustomerProfile("Original Name", "MEM123", "original@example.com", originalPhone, "user1");

        // Act
        profile.Update("avatar.jpg", "New Name", emptyString, birthday: null, "user2");

        // Assert
        profile.Name.ShouldBe("New Name"); // Name should update
        profile.Phone.ShouldBe(originalPhone);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void CustomerProfileUpdateWithWhitespaceStringsShouldUpdateBothFields(string whitespaceString)
    {
        // Arrange - string.IsNullOrEmpty returns false for whitespace, so these will actually update
        var originalName = "Original Name";
        var originalPhone = "+1111111111";
        var profile = new CustomerProfile(originalName, "MEM123", "original@example.com", originalPhone, "user1");

        // Act
        profile.Update("avatar.jpg", whitespaceString, whitespaceString, birthday: null, "user2");

        // Assert - Since string.IsNullOrEmpty(whitespace) is false, they will be updated
        profile.Name.ShouldBe(whitespaceString);
        profile.Phone.ShouldBe(whitespaceString);
    }

    [Fact]
    public void AddressAllPropertiesRequiredShouldSetCorrectly()
    {
        // Arrange
        var line = "123 Main Street";
        var state = "California";
        var city = "Los Angeles";
        var country = "United States";
        var postal = "90210";

        // Act
        var address = new Address(line, state, city, country, postal);

        // Assert
        address.Line.ShouldBe(line);
        address.State.ShouldBe(state);
        address.City.ShouldBe(city);
        address.Country.ShouldBe(country);
        address.Postal.ShouldBe(postal);
    }

    [Fact]
    public void CompanyAllPropertiesSetShouldSetCorrectly()
    {
        // Arrange
        var name = "Tech Solutions Inc";
        var uen = "UEN123456789";
        var abn = "ABN123456789";
        var arbn = "ARBN123456789";
        var can = "CAN123456789";

        // Act
        var company = new Company(name, uen, abn, arbn, can);

        // Assert
        company.Name.ShouldBe(name);
        company.UEN.ShouldBe(uen);
        company.ABN.ShouldBe(abn);
        company.ARBN.ShouldBe(arbn);
        company.CAN.ShouldBe(can);
    }

    [Fact]
    public void EmployeePromoteToShouldChangeTypeCorrectly()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var employee = new Employee(profileId, EmployeeType.Secretary, "user1");
        var originalType = employee.Type;

        // Act
        employee.PromoteTo(EmployeeType.Director, "user2");

        // Assert
        employee.Type.ShouldBe(EmployeeType.Director);
        employee.Type.ShouldNotBe(originalType);
        employee.ProfileId.ShouldBe(profileId);
    }

    [Fact]
    public void EmployeeMultiplePomotionsShouldWorkCorrectly()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var employee = new Employee(profileId, EmployeeType.Other, "user1");

        // Act & Assert - Multiple promotions
        employee.PromoteTo(EmployeeType.Secretary, "user2");
        employee.Type.ShouldBe(EmployeeType.Secretary);

        employee.PromoteTo(EmployeeType.Director, "user3");
        employee.Type.ShouldBe(EmployeeType.Director);

        employee.PromoteTo(EmployeeType.Other, "user4");
        employee.Type.ShouldBe(EmployeeType.Other);
    }

    [Theory]
    [InlineData(EmployeeType.None)]
    [InlineData(EmployeeType.Director)]
    [InlineData(EmployeeType.Secretary)]
    [InlineData(EmployeeType.Other)]
    public void EmployeeCanBePromotedToAnyType(EmployeeType targetType)
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var employee = new Employee(profileId, EmployeeType.None, "user1");

        // Act
        employee.PromoteTo(targetType, "user2");

        // Assert
        employee.Type.ShouldBe(targetType);
    }
}