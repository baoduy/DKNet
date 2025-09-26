namespace EfCore.Repos.Tests;

/// <summary>
/// Tests for the OrSpecification class functionality
/// </summary>
public class OrSpecificationTests
{
    [Fact]
    public void Constructor_WithBothSpecificationsHavingFilters_ShouldCombineWithOr()
    {
        // Arrange
        var leftSpec = new TestSpecification();
        leftSpec.AddTestFilter(u => u.FirstName == "John");
        
        var rightSpec = new TestSpecification();
        rightSpec.AddTestFilter(u => u.FirstName == "Jane");

        // Act
        var orSpec = new OrSpecification<User>(leftSpec, rightSpec);

        // Assert
        orSpec.FilterQuery.ShouldNotBeNull();
        
        // Test combined filter with matching entity (left condition)
        var matchingUser1 = new User("test") { FirstName = "John", LastName = "Doe" };
        orSpec.Match(matchingUser1).ShouldBeTrue();
        
        // Test combined filter with matching entity (right condition)
        var matchingUser2 = new User("test") { FirstName = "Jane", LastName = "Smith" };
        orSpec.Match(matchingUser2).ShouldBeTrue();
        
        // Test combined filter with non-matching entity
        var nonMatchingUser = new User("test") { FirstName = "Bob", LastName = "Wilson" };
        orSpec.Match(nonMatchingUser).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithLeftSpecificationNull_ShouldUseRightSpecification()
    {
        // Arrange
        var leftSpec = new TestSpecification(); // No filter
        var rightSpec = new TestSpecification();
        rightSpec.AddTestFilter(u => u.LastName == "Doe");

        // Act
        var orSpec = new OrSpecification<User>(leftSpec, rightSpec);

        // Assert
        orSpec.FilterQuery.ShouldNotBeNull();
        
        var user = new User("test") { FirstName = "John", LastName = "Doe" };
        orSpec.Match(user).ShouldBeTrue();
        
        var user2 = new User("test") { FirstName = "John", LastName = "Smith" };
        orSpec.Match(user2).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithRightSpecificationNull_ShouldUseLeftSpecification()
    {
        // Arrange
        var leftSpec = new TestSpecification();
        leftSpec.AddTestFilter(u => u.FirstName == "John");
        
        var rightSpec = new TestSpecification(); // No filter

        // Act
        var orSpec = new OrSpecification<User>(leftSpec, rightSpec);

        // Assert
        orSpec.FilterQuery.ShouldNotBeNull();
        
        var user = new User("test") { FirstName = "John", LastName = "Doe" };
        orSpec.Match(user).ShouldBeTrue();
        
        var user2 = new User("test") { FirstName = "Jane", LastName = "Doe" };
        orSpec.Match(user2).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithBothSpecificationsNull_ShouldHaveNullFilter()
    {
        // Arrange
        var leftSpec = new TestSpecification(); // No filter
        var rightSpec = new TestSpecification(); // No filter

        // Act
        var orSpec = new OrSpecification<User>(leftSpec, rightSpec);

        // Assert
        orSpec.FilterQuery.ShouldBeNull();
        
        var user = new User("test") { FirstName = "John", LastName = "Doe" };
        orSpec.Match(user).ShouldBeFalse(); // Returns false when FilterQuery is null
    }

    [Fact]
    public void Constructor_WithComplexExpressions_ShouldCombineCorrectly()
    {
        // Arrange
        var leftSpec = new TestSpecification();
        leftSpec.AddTestFilter(u => u.FirstName.StartsWith("J"));
        
        var rightSpec = new TestSpecification();
        rightSpec.AddTestFilter(u => u.LastName.Length < 4);

        // Act
        var orSpec = new OrSpecification<User>(leftSpec, rightSpec);

        // Assert
        orSpec.FilterQuery.ShouldNotBeNull();
        
        // Test with user that matches first condition
        var matchingUser1 = new User("test") { FirstName = "John", LastName = "Johnson" }; // J* (true) and length < 4 (false)
        orSpec.Match(matchingUser1).ShouldBeTrue();
        
        // Test with user that matches second condition
        var matchingUser2 = new User("test") { FirstName = "Mike", LastName = "Doe" }; // J* (false) and length < 4 (true)
        orSpec.Match(matchingUser2).ShouldBeTrue();
        
        // Test with user that matches both conditions
        var matchingUser3 = new User("test") { FirstName = "Jim", LastName = "Lee" }; // J* (true) and length < 4 (true)
        orSpec.Match(matchingUser3).ShouldBeTrue();
        
        // Test with user that matches neither condition
        var nonMatchingUser = new User("test") { FirstName = "Mike", LastName = "Johnson" }; // J* (false) and length < 4 (false)
        orSpec.Match(nonMatchingUser).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithNestedOrSpecifications_ShouldWork()
    {
        // Arrange
        var spec1 = new TestSpecification();
        spec1.AddTestFilter(u => u.FirstName == "John");
        
        var spec2 = new TestSpecification();
        spec2.AddTestFilter(u => u.FirstName == "Jane");
        
        var spec3 = new TestSpecification();
        spec3.AddTestFilter(u => u.FirstName == "Bob");

        var orSpec1 = new OrSpecification<User>(spec1, spec2);

        // Act
        var nestedOrSpec = new OrSpecification<User>(orSpec1, spec3);

        // Assert
        nestedOrSpec.FilterQuery.ShouldNotBeNull();
        
        // Test with user that matches first condition
        var matchingUser1 = new User("test") { FirstName = "John", LastName = "Doe" };
        nestedOrSpec.Match(matchingUser1).ShouldBeTrue();
        
        // Test with user that matches second condition
        var matchingUser2 = new User("test") { FirstName = "Jane", LastName = "Smith" };
        nestedOrSpec.Match(matchingUser2).ShouldBeTrue();
        
        // Test with user that matches third condition
        var matchingUser3 = new User("test") { FirstName = "Bob", LastName = "Wilson" };
        nestedOrSpec.Match(matchingUser3).ShouldBeTrue();
        
        // Test with user that doesn't match any condition
        var nonMatchingUser = new User("test") { FirstName = "Mike", LastName = "Johnson" };
        nestedOrSpec.Match(nonMatchingUser).ShouldBeFalse();
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
        var orSpec = new OrSpecification<User>(spec1, spec2);

        // Assert
        orSpec.FilterQuery.ShouldNotBeNull();
        
        var matchingUser = new User("test") { FirstName = "John", LastName = "Doe" };
        orSpec.Match(matchingUser).ShouldBeTrue();
        
        var nonMatchingUser = new User("test") { FirstName = "Jane", LastName = "Doe" };
        orSpec.Match(nonMatchingUser).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithMixedAndOrSpecifications_ShouldWork()
    {
        // Arrange
        var spec1 = new TestSpecification();
        spec1.AddTestFilter(u => u.FirstName == "John");
        
        var spec2 = new TestSpecification();
        spec2.AddTestFilter(u => u.LastName == "Doe");
        
        var spec3 = new TestSpecification();
        spec3.AddTestFilter(u => u.FirstName == "Jane");

        var andSpec = new AndSpecification<User>(spec1, spec2); // John AND Doe

        // Act
        var orSpec = new OrSpecification<User>(andSpec, spec3); // (John AND Doe) OR Jane

        // Assert
        orSpec.FilterQuery.ShouldNotBeNull();
        
        // Test with user that matches AND condition
        var matchingUser1 = new User("test") { FirstName = "John", LastName = "Doe" };
        orSpec.Match(matchingUser1).ShouldBeTrue();
        
        // Test with user that matches OR condition
        var matchingUser2 = new User("test") { FirstName = "Jane", LastName = "Smith" };
        orSpec.Match(matchingUser2).ShouldBeTrue();
        
        // Test with user that matches neither condition
        var nonMatchingUser = new User("test") { FirstName = "Bob", LastName = "Wilson" };
        orSpec.Match(nonMatchingUser).ShouldBeFalse();
    }
}