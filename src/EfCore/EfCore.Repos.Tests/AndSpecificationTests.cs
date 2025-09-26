namespace EfCore.Repos.Tests;

/// <summary>
/// Tests for the AndSpecification class functionality
/// </summary>
public class AndSpecificationTests
{
    [Fact]
    public void Constructor_WithBothSpecificationsHavingFilters_ShouldCombineWithAnd()
    {
        // Arrange
        var leftSpec = new TestSpecification();
        leftSpec.AddTestFilter(u => u.FirstName == "John");
        
        var rightSpec = new TestSpecification();
        rightSpec.AddTestFilter(u => u.LastName == "Doe");

        // Act
        var andSpec = new AndSpecification<User>(leftSpec, rightSpec);

        // Assert
        andSpec.FilterQuery.ShouldNotBeNull();
        
        // Test combined filter with matching entity
        var matchingUser = new User("test") { FirstName = "John", LastName = "Doe" };
        andSpec.Match(matchingUser).ShouldBeTrue();
        
        // Test combined filter with non-matching entity (wrong first name)
        var nonMatchingUser1 = new User("test") { FirstName = "Jane", LastName = "Doe" };
        andSpec.Match(nonMatchingUser1).ShouldBeFalse();
        
        // Test combined filter with non-matching entity (wrong last name)
        var nonMatchingUser2 = new User("test") { FirstName = "John", LastName = "Smith" };
        andSpec.Match(nonMatchingUser2).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithLeftSpecificationNull_ShouldUseRightSpecification()
    {
        // Arrange
        var leftSpec = new TestSpecification(); // No filter
        var rightSpec = new TestSpecification();
        rightSpec.AddTestFilter(u => u.LastName == "Doe");

        // Act
        var andSpec = new AndSpecification<User>(leftSpec, rightSpec);

        // Assert
        andSpec.FilterQuery.ShouldNotBeNull();
        
        var user = new User("test") { FirstName = "John", LastName = "Doe" };
        andSpec.Match(user).ShouldBeTrue();
        
        var user2 = new User("test") { FirstName = "John", LastName = "Smith" };
        andSpec.Match(user2).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithRightSpecificationNull_ShouldUseLeftSpecification()
    {
        // Arrange
        var leftSpec = new TestSpecification();
        leftSpec.AddTestFilter(u => u.FirstName == "John");
        
        var rightSpec = new TestSpecification(); // No filter

        // Act
        var andSpec = new AndSpecification<User>(leftSpec, rightSpec);

        // Assert
        andSpec.FilterQuery.ShouldNotBeNull();
        
        var user = new User("test") { FirstName = "John", LastName = "Doe" };
        andSpec.Match(user).ShouldBeTrue();
        
        var user2 = new User("test") { FirstName = "Jane", LastName = "Doe" };
        andSpec.Match(user2).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithBothSpecificationsNull_ShouldHaveNullFilter()
    {
        // Arrange
        var leftSpec = new TestSpecification(); // No filter
        var rightSpec = new TestSpecification(); // No filter

        // Act
        var andSpec = new AndSpecification<User>(leftSpec, rightSpec);

        // Assert
        andSpec.FilterQuery.ShouldBeNull();
        
        var user = new User("test") { FirstName = "John", LastName = "Doe" };
        andSpec.Match(user).ShouldBeFalse(); // Returns false when FilterQuery is null
    }

    [Fact]
    public void Constructor_WithComplexExpressions_ShouldCombineCorrectly()
    {
        // Arrange
        var leftSpec = new TestSpecification();
        leftSpec.AddTestFilter(u => u.FirstName.StartsWith("J"));
        
        var rightSpec = new TestSpecification();
        rightSpec.AddTestFilter(u => u.LastName.Length > 3);

        // Act
        var andSpec = new AndSpecification<User>(leftSpec, rightSpec);

        // Assert
        andSpec.FilterQuery.ShouldNotBeNull();
        
        // Test with user that matches both conditions
        var matchingUser = new User("test") { FirstName = "John", LastName = "Smith" }; // J* and length > 3
        andSpec.Match(matchingUser).ShouldBeTrue();
        
        // Test with user that matches first but not second
        var nonMatchingUser1 = new User("test") { FirstName = "John", LastName = "Do" }; // J* but length <= 3
        andSpec.Match(nonMatchingUser1).ShouldBeFalse();
        
        // Test with user that matches second but not first
        var nonMatchingUser2 = new User("test") { FirstName = "Mike", LastName = "Johnson" }; // Not J* but length > 3
        andSpec.Match(nonMatchingUser2).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithNestedAndSpecifications_ShouldWork()
    {
        // Arrange
        var spec1 = new TestSpecification();
        spec1.AddTestFilter(u => u.FirstName == "John");
        
        var spec2 = new TestSpecification();
        spec2.AddTestFilter(u => u.LastName == "Doe");
        
        var spec3 = new TestSpecification();
        spec3.AddTestFilter(u => u.FirstName.Length > 2);

        var andSpec1 = new AndSpecification<User>(spec1, spec2);

        // Act
        var nestedAndSpec = new AndSpecification<User>(andSpec1, spec3);

        // Assert
        nestedAndSpec.FilterQuery.ShouldNotBeNull();
        
        // Test with user that matches all three conditions
        var matchingUser = new User("test") { FirstName = "John", LastName = "Doe" };
        nestedAndSpec.Match(matchingUser).ShouldBeTrue();
        
        // Test with user that doesn't match first condition
        var nonMatchingUser = new User("test") { FirstName = "Jane", LastName = "Doe" };
        nestedAndSpec.Match(nonMatchingUser).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithIdenticalSpecifications_ShouldWork()
    {
        // Arrange
        var spec1 = new TestSpecification();
        spec1.AddTestFilter(u => u.FirstName == "John");
        
        var spec2 = new TestSpecification();
        spec2.AddTestFilter(u => u.FirstName == "John");

        // Act
        var andSpec = new AndSpecification<User>(spec1, spec2);

        // Assert
        andSpec.FilterQuery.ShouldNotBeNull();
        
        var matchingUser = new User("test") { FirstName = "John", LastName = "Doe" };
        andSpec.Match(matchingUser).ShouldBeTrue();
        
        var nonMatchingUser = new User("test") { FirstName = "Jane", LastName = "Doe" };
        andSpec.Match(nonMatchingUser).ShouldBeFalse();
    }
}