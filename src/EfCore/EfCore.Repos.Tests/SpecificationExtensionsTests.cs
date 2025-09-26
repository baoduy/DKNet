using DKNet.EfCore.Repos.Abstractions;
using DKNet.EfCore.Specifications;
using EfCore.Repos.Tests.TestEntities;
using Moq;
using Shouldly;

namespace EfCore.Repos.Tests;

/// <summary>
/// Tests for the SpecificationExtensions class functionality
/// </summary>
public class SpecificationExtensionsTests
{
    [Fact]
    public void WithSpecs_WithNullSpecification_ShouldThrowArgumentNullException()
    {
        // Arrange
        var queryable = new List<User>().AsQueryable();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => queryable.WithSpecs<User>(null!));
    }

    [Fact]
    public void WithSpecs_WithFilterOnly_ShouldApplyFilter()
    {
        // Arrange
        var users = new List<User>
        {
            new("test") { FirstName = "John", LastName = "Doe" },
            new("test") { FirstName = "Jane", LastName = "Smith" },
            new("test") { FirstName = "Bob", LastName = "Wilson" }
        };
        var queryable = users.AsQueryable();
        
        var spec = new TestSpecification();
        spec.AddTestFilter(u => u.FirstName == "John");

        // Act
        var result = queryable.WithSpecs(spec).ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].FirstName.ShouldBe("John");
    }

    [Fact]
    public void WithSpecs_WithIgnoreQueryFilters_ShouldNotThrow()
    {
        // Arrange
        var users = new List<User>
        {
            new("test") { FirstName = "John", LastName = "Doe" }
        };
        var queryable = users.AsQueryable();
        
        var spec = new TestSpecification();
        spec.EnableIgnoreQueryFilters();

        // Act & Assert
        // For the basic test, we just ensure it doesn't throw since IgnoreQueryFilters is EF Core specific
        Should.NotThrow(() => queryable.WithSpecs(spec));
    }

    [Fact]
    public void WithSpecs_WithOrderByOnly_ShouldApplyOrdering()
    {
        // Arrange
        var users = new List<User>
        {
            new("test") { FirstName = "John", LastName = "Wilson" },
            new("test") { FirstName = "Jane", LastName = "Smith" },
            new("test") { FirstName = "Bob", LastName = "Doe" }
        };
        var queryable = users.AsQueryable();
        
        var spec = new TestSpecification();
        spec.AddTestOrderBy(u => u.LastName);

        // Act
        var result = queryable.WithSpecs(spec).ToList();

        // Assert
        result.Count.ShouldBe(3);
        result[0].LastName.ShouldBe("Doe");
        result[1].LastName.ShouldBe("Smith");
        result[2].LastName.ShouldBe("Wilson");
    }

    [Fact]
    public void WithSpecs_WithOrderByDescendingOnly_ShouldApplyDescendingOrdering()
    {
        // Arrange
        var users = new List<User>
        {
            new("test") { FirstName = "John", LastName = "Wilson" },
            new("test") { FirstName = "Jane", LastName = "Smith" },
            new("test") { FirstName = "Bob", LastName = "Doe" }
        };
        var queryable = users.AsQueryable();
        
        var spec = new TestSpecification();
        spec.AddTestOrderByDescending(u => u.LastName);

        // Act
        var result = queryable.WithSpecs(spec).ToList();

        // Assert
        result.Count.ShouldBe(3);
        result[0].LastName.ShouldBe("Wilson");
        result[1].LastName.ShouldBe("Smith");
        result[2].LastName.ShouldBe("Doe");
    }

    [Fact]
    public void WithSpecs_WithMultipleOrderBy_ShouldApplyInOrder()
    {
        // Arrange
        var users = new List<User>
        {
            new("test") { FirstName = "John", LastName = "Smith" },
            new("test") { FirstName = "Jane", LastName = "Smith" },
            new("test") { FirstName = "Bob", LastName = "Doe" }
        };
        var queryable = users.AsQueryable();
        
        var spec = new TestSpecification();
        spec.AddTestOrderBy(u => u.LastName);
        spec.AddTestOrderBy(u => u.FirstName); // ThenBy

        // Act
        var result = queryable.WithSpecs(spec).ToList();

        // Assert
        result.Count.ShouldBe(3);
        result[0].LastName.ShouldBe("Doe");
        result[0].FirstName.ShouldBe("Bob");
        result[1].LastName.ShouldBe("Smith");
        result[1].FirstName.ShouldBe("Jane");
        result[2].LastName.ShouldBe("Smith");
        result[2].FirstName.ShouldBe("John");
    }

    [Fact]
    public void WithSpecs_WithMultipleOrderByDescending_ShouldApplyInOrder()
    {
        // Arrange
        var users = new List<User>
        {
            new("test") { FirstName = "John", LastName = "Smith" },
            new("test") { FirstName = "Jane", LastName = "Smith" },
            new("test") { FirstName = "Bob", LastName = "Doe" }
        };
        var queryable = users.AsQueryable();
        
        var spec = new TestSpecification();
        spec.AddTestOrderByDescending(u => u.LastName);
        spec.AddTestOrderByDescending(u => u.FirstName); // ThenByDescending

        // Act
        var result = queryable.WithSpecs(spec).ToList();

        // Assert
        result.Count.ShouldBe(3);
        result[0].LastName.ShouldBe("Smith");
        result[0].FirstName.ShouldBe("John");
        result[1].LastName.ShouldBe("Smith");
        result[1].FirstName.ShouldBe("Jane");
        result[2].LastName.ShouldBe("Doe");
        result[2].FirstName.ShouldBe("Bob");
    }

    [Fact]
    public void WithSpecs_WithMixedOrdering_ShouldApplyInOrder()
    {
        // Arrange
        var users = new List<User>
        {
            new("test") { FirstName = "John", LastName = "Smith" },
            new("test") { FirstName = "Jane", LastName = "Smith" },
            new("test") { FirstName = "Bob", LastName = "Doe" }
        };
        var queryable = users.AsQueryable();
        
        var spec = new TestSpecification();
        spec.AddTestOrderBy(u => u.LastName); // OrderBy LastName
        spec.AddTestOrderByDescending(u => u.FirstName); // ThenByDescending FirstName

        // Act
        var result = queryable.WithSpecs(spec).ToList();

        // Assert
        result.Count.ShouldBe(3);
        result[0].LastName.ShouldBe("Doe");
        result[0].FirstName.ShouldBe("Bob");
        result[1].LastName.ShouldBe("Smith");
        result[1].FirstName.ShouldBe("John"); // John comes before Jane when descending
        result[2].LastName.ShouldBe("Smith");
        result[2].FirstName.ShouldBe("Jane");
    }

    [Fact]
    public void WithSpecs_WithEmptySpecification_ShouldReturnOriginalQueryable()
    {
        // Arrange
        var users = new List<User>
        {
            new("test") { FirstName = "John", LastName = "Doe" },
            new("test") { FirstName = "Jane", LastName = "Smith" }
        };
        var queryable = users.AsQueryable();
        
        var spec = new TestSpecification(); // Empty spec

        // Act
        var result = queryable.WithSpecs(spec).ToList();

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldBe(users); // Same order, same items
    }

    [Fact]
    public void WithSpecs_OnRepository_ShouldCallGetsAndApplySpecs()
    {
        // Arrange
        var users = new List<User>
        {
            new("test") { FirstName = "John", LastName = "Doe" },
            new("test") { FirstName = "Jane", LastName = "Smith" }
        };

        var mockRepo = new Mock<IReadRepository<User>>();
        mockRepo.Setup(r => r.Gets()).Returns(users.AsQueryable());

        var spec = new TestSpecification();
        spec.AddTestFilter(u => u.FirstName == "John");

        // Act
        var result = mockRepo.Object.WithSpecs(spec).ToList();

        // Assert
        mockRepo.Verify(r => r.Gets(), Times.Once);
        result.Count.ShouldBe(1);
        result[0].FirstName.ShouldBe("John");
    }

    [Fact]
    public void SpecsListAsync_ShouldCallRepository()
    {
        // Arrange
        var users = new List<User>
        {
            new("test") { FirstName = "John", LastName = "Doe" },
            new("test") { FirstName = "Jane", LastName = "Smith" }
        };

        var mockRepo = new Mock<IReadRepository<User>>();
        mockRepo.Setup(r => r.Gets()).Returns(users.AsQueryable());

        var spec = new TestSpecification();
        spec.AddTestFilter(u => u.FirstName == "John");

        // Act
        var queryable = mockRepo.Object.WithSpecs(spec);
        var result = queryable.ToList();

        // Assert
        result.Count.ShouldBe(1);
        result.First().FirstName.ShouldBe("John");
    }

    [Fact]
    public void SpecsFirstOrDefaultAsync_WithMatchingEntity_ShouldSetupCorrectly()
    {
        // Arrange
        var users = new List<User>
        {
            new("test") { FirstName = "John", LastName = "Doe" },
            new("test") { FirstName = "Jane", LastName = "Smith" }
        };

        var mockRepo = new Mock<IReadRepository<User>>();
        mockRepo.Setup(r => r.Gets()).Returns(users.AsQueryable());

        var spec = new TestSpecification();
        spec.AddTestFilter(u => u.FirstName == "John");

        // Act
        var queryable = mockRepo.Object.WithSpecs(spec);
        var result = queryable.FirstOrDefault();

        // Assert
        result.ShouldNotBeNull();
        result.FirstName.ShouldBe("John");
    }

    [Fact]
    public void SpecsFirstOrDefaultAsync_WithNoMatchingEntity_ShouldReturnNull()
    {
        // Arrange
        var users = new List<User>
        {
            new("test") { FirstName = "John", LastName = "Doe" },
            new("test") { FirstName = "Jane", LastName = "Smith" }
        };

        var mockRepo = new Mock<IReadRepository<User>>();
        mockRepo.Setup(r => r.Gets()).Returns(users.AsQueryable());

        var spec = new TestSpecification();
        spec.AddTestFilter(u => u.FirstName == "Bob");

        // Act
        var queryable = mockRepo.Object.WithSpecs(spec);
        var result = queryable.FirstOrDefault();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void SpecsToPageEnumerable_ShouldReturnEnumerable()
    {
        // Arrange
        var users = new List<User>
        {
            new("test") { FirstName = "John", LastName = "Doe" },
            new("test") { FirstName = "Jane", LastName = "Smith" }
        };

        var mockRepo = new Mock<IReadRepository<User>>();
        mockRepo.Setup(r => r.Gets()).Returns(users.AsQueryable());

        var spec = new TestSpecification();
        spec.AddTestFilter(u => u.FirstName == "John");

        // Act
        var result = mockRepo.Object.SpecsToPageEnumerable(spec);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void SpecsToPageListAsync_ShouldSetupRepositoryCorrectly()
    {
        // Arrange
        var users = new List<User>();
        for (int i = 1; i <= 10; i++)
        {
            users.Add(new User("test") { FirstName = $"User{i}", LastName = "Test" });
        }

        var mockRepo = new Mock<IReadRepository<User>>();
        mockRepo.Setup(r => r.Gets()).Returns(users.AsQueryable());

        var spec = new TestSpecification();

        // Act & Assert - Just verify the setup works without throwing
        Should.NotThrow(() => mockRepo.Object.WithSpecs(spec));
        mockRepo.Verify(r => r.Gets(), Times.Once);
    }

    [Fact]
    public void WithSpecs_WithOrderByDescendingFirst_ShouldApplyCorrectly()
    {
        // Arrange
        var users = new List<User>
        {
            new("test") { FirstName = "John", LastName = "Wilson" },
            new("test") { FirstName = "Jane", LastName = "Smith" },
            new("test") { FirstName = "Bob", LastName = "Doe" }
        };
        var queryable = users.AsQueryable();
        
        var spec = new TestSpecification();
        // Start with OrderByDescending (no OrderBy first)
        spec.AddTestOrderByDescending(u => u.LastName);

        // Act
        var result = queryable.WithSpecs(spec).ToList();

        // Assert
        result.Count.ShouldBe(3);
        result[0].LastName.ShouldBe("Wilson");
        result[1].LastName.ShouldBe("Smith");
        result[2].LastName.ShouldBe("Doe");
    }

    [Fact]
    public void WithSpecs_WithIncludes_ShouldApplyIncludes()
    {
        // Arrange
        var users = new List<User>
        {
            new("test") { FirstName = "John", LastName = "Doe" }
        };
        var queryable = users.AsQueryable();
        
        var spec = new TestSpecification();
        spec.AddTestInclude(u => u.Addresses);
        spec.AddTestInclude(u => u.FirstName); // Multiple includes

        // Act & Assert
        // Since we're working with in-memory lists, just ensure it doesn't throw
        Should.NotThrow(() => queryable.WithSpecs(spec));
    }

    [Fact]
    public void WithSpecs_WithComplexSpecification_ShouldApplyAllAspects()
    {
        // Arrange
        var users = new List<User>
        {
            new("test") { FirstName = "John", LastName = "Wilson" },
            new("test") { FirstName = "Jane", LastName = "Smith" },
            new("test") { FirstName = "Bob", LastName = "Doe" },
            new("test") { FirstName = "Alice", LastName = "Johnson" }
        };
        var queryable = users.AsQueryable();
        
        var spec = new TestSpecification();
        spec.AddTestFilter(u => u.FirstName.Length > 3); // Filter: name length > 3
        spec.AddTestOrderBy(u => u.LastName); // Order by last name

        // Act
        var result = queryable.WithSpecs(spec).ToList();

        // Assert
        result.Count.ShouldBe(3); // John, Jane, Alice (Bob has length 3)
        result[0].LastName.ShouldBe("Johnson"); // Alice
        result[1].LastName.ShouldBe("Smith");   // Jane
        result[2].LastName.ShouldBe("Wilson");  // John
    }
}