using SlimBus.Domains.Features.Profiles.Entities;

namespace SlimBus.App.Tests.Unit;

public class DomainEntityTests
{
    [Fact]
    public void CustomerProfileConstructorShouldSetPropertiesCorrectly()
    {
        // Arrange
        var name = "John Doe";
        var membershipNo = "MEM12345";
        var email = "john.doe@example.com";
        var phone = "+1234567890";
        var userId = "user123";

        // Act
        var profile = new CustomerProfile(name, membershipNo, email, phone, userId);

        // Assert
        profile.Name.ShouldBe(name);
        profile.MembershipNo.ShouldBe(membershipNo);
        profile.Email.ShouldBe(email);
        profile.Phone.ShouldBe(phone);
        // Note: The parameterless constructor uses Guid.Empty internally
        profile.Id.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void CustomerProfileConstructorWithIdShouldSetIdCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Jane Doe";
        var membershipNo = "MEM54321";
        var email = "jane.doe@example.com";
        var phone = "+0987654321";
        var userId = "user456";

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
    public void CustomerProfileUpdateShouldUpdatePropertiesCorrectly()
    {
        // Arrange
        var profile = new CustomerProfile("Original Name", "MEM123", "orig@example.com", "+1111111111", "user1");
        var newAvatar = "new-avatar.jpg";
        var newName = "Updated Name";
        var newPhone = "+2222222222";
        var newBirthday = new DateTime(1990, 5, 15, 0, 0, 0, DateTimeKind.Local);
        var userId = "user2";

        // Act
        profile.Update(newAvatar, newName, newPhone, newBirthday, userId);

        // Assert
        profile.Avatar.ShouldBe(newAvatar);
        profile.Name.ShouldBe(newName);
        profile.Phone.ShouldBe(newPhone);
        profile.BirthDay.ShouldBe(newBirthday);
    }

    [Fact]
    public void CustomerProfileUpdateWithNullOrEmptyNameShouldNotUpdateName()
    {
        // Arrange
        var originalName = "Original Name";
        var profile = new CustomerProfile(originalName, "MEM123", "orig@example.com", "+1111111111", "user1");

        // Act
        profile.Update("avatar.jpg", null, "+2222222222", null, "user2");

        // Assert
        profile.Name.ShouldBe(originalName);
        profile.Phone.ShouldBe("+2222222222");
    }

    [Fact]
    public void CustomerProfileUpdateWithNullOrEmptyPhoneShouldNotUpdatePhone()
    {
        // Arrange
        var originalPhone = "+1111111111";
        var profile = new CustomerProfile("Name", "MEM123", "orig@example.com", originalPhone, "user1");

        // Act
        profile.Update("avatar.jpg", "New Name", string.Empty, null, "user2");

        // Assert
        profile.Name.ShouldBe("New Name");
        profile.Phone.ShouldBe(originalPhone);
    }

    [Fact]
    public void AddressConstructorShouldSetPropertiesCorrectly()
    {
        // Arrange
        var line = "123 Main St";
        var state = "CA";
        var city = "Los Angeles";
        var country = "USA";
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

    [Theory]
    [InlineData("123 Main St", "CA", "Los Angeles", "USA", "90210")]
    [InlineData("456 Oak Ave", "NY", "New York", "USA", "10001")]
    [InlineData("789 Pine Rd", "TX", "Houston", "USA", "77001")]
    public void AddressConstructorWithDifferentValuesShouldWork(string line, string state, string city, string country,
        string postal)
    {
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
    public void CompanyConstructorShouldSetPropertiesCorrectly()
    {
        // Arrange
        var name = "Acme Corp";
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

    [Theory]
    [InlineData("Tech Solutions Inc", "UEN987654321", "ABN987654321", "ARBN987654321", "CAN987654321")]
    [InlineData("Global Services Ltd", "UEN111222333", "ABN111222333", "ARBN111222333", "CAN111222333")]
    public void CompanyConstructorWithDifferentValuesShouldWork(string name, string uen, string abn, string arbn,
        string can)
    {
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
    public void EmployeeConstructorShouldSetPropertiesCorrectly()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var type = EmployeeType.Director;
        var userId = "user123";

        // Act
        var employee = new Employee(profileId, type, userId);

        // Assert
        employee.ProfileId.ShouldBe(profileId);
        employee.Type.ShouldBe(type);
        employee.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(EmployeeType.Director)]
    [InlineData(EmployeeType.Secretary)]
    [InlineData(EmployeeType.Other)]
    [InlineData(EmployeeType.None)]
    public void EmployeeConstructorWithDifferentTypesShouldWork(EmployeeType type)
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var userId = "user123";

        // Act
        var employee = new Employee(profileId, type, userId);

        // Assert
        employee.Type.ShouldBe(type);
        employee.ProfileId.ShouldBe(profileId);
    }

    [Fact]
    public void EmployeePromoteToShouldUpdateType()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var employee = new Employee(profileId, EmployeeType.Secretary, "user1");
        var newType = EmployeeType.Director;
        var userId = "user2";

        // Act
        employee.PromoteTo(newType, userId);

        // Assert
        employee.Type.ShouldBe(newType);
    }

    [Theory]
    [InlineData(EmployeeType.Secretary, EmployeeType.Director)]
    [InlineData(EmployeeType.Other, EmployeeType.Secretary)]
    [InlineData(EmployeeType.Director, EmployeeType.Other)]
    public void EmployeePromoteToWithDifferentTypesShouldWork(EmployeeType initialType, EmployeeType newType)
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var employee = new Employee(profileId, initialType, "user1");
        var userId = "user2";

        // Act
        employee.PromoteTo(newType, userId);

        // Assert
        employee.Type.ShouldBe(newType);
    }
}