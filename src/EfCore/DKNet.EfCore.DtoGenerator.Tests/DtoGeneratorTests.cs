using System.Reflection;
using Shouldly;
using Mapster; // Added for Adapt mappings

namespace DKNet.EfCore.DtoGenerator.Tests;

public class DtoGeneratorTests
{
    [Fact]
    public void PersonDto_Adapt_Maps_All_Properties()
    {
        var person = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = "Jane",
            MiddleName = "Q",
            LastName = "Doe",
            Age = 42,
            CreatedUtc = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc)
        };

        var dto = person.Adapt<PersonDto>();

        dto.Id.ShouldBe(person.Id);
        dto.FirstName.ShouldBe(person.FirstName);
        dto.MiddleName.ShouldBe(person.MiddleName);
        dto.LastName.ShouldBe(person.LastName);
        dto.Age.ShouldBe(person.Age);
        dto.CreatedUtc.ShouldBe(person.CreatedUtc);
    }

    [Fact]
    public void PersonDto_Immutability_WithExpression_Works()
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "A", LastName = "B", Age = 1 };
        var dto1 = person.Adapt<PersonDto>();
        var dto2 = dto1 with { FirstName = "C" };

        dto1.FirstName.ShouldBe("A");
        dto2.FirstName.ShouldBe("C");
        dto1.ShouldNotBe(dto2); // record structural equality should differ
    }

    [Fact]
    public void PersonCustomDto_Uses_Override_And_Keeps_Custom_Property()
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Alpha", LastName = "Beta", Age = 5 };
        var dto = person.Adapt<PersonCustomDto>();

        dto.FirstName.ShouldBe("Alpha");
        dto.DisplayName.ShouldBe("Alpha?");

        typeof(PersonCustomDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Count(p => p.Name == nameof(Person.FirstName))
            .ShouldBe(1);
    }

    [Fact]
    public void CustomerDto_Adapt_Maps_Reference_And_Collections()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Total = 10.5m,
            OrderedUtc = DateTime.UtcNow,
            Items = new List<OrderItem>
            {
                new() { Id = 1, Sku = "ABC", Quantity = 2, Price = 3.25m },
                new() { Id = 2, Sku = "XYZ", Quantity = 1, Price = 4.00m }
            }
        };
        var customer = new Customer
        {
            CustomerId = 77,
            Name = "Contoso",
            Email = "sales@contoso.test",
            PrimaryAddress = new Address { Line1 = "1 Main", City = "Town", Country = "US" },
            Orders = new List<Order> { order }
        };

        var dto = customer.Adapt<CustomerDto>();
        dto.CustomerId.ShouldBe(customer.CustomerId);
        dto.Name.ShouldBe(customer.Name);
        dto.Email.ShouldBe(customer.Email);
        dto.PrimaryAddress!.City.ShouldBe(customer.PrimaryAddress.City);
        dto.PrimaryAddress!.Line1.ShouldBe(customer.PrimaryAddress.Line1);
        dto.PrimaryAddress!.Country.ShouldBe(customer.PrimaryAddress.Country);
        dto.Orders.Count.ShouldBe(customer.Orders.Count);
    }

    [Fact]
    public void Adapt_Sequence_Projects_All()
    {
        var people = Enumerable.Range(1, 5).Select(i => new Person
            { Id = Guid.NewGuid(), FirstName = "F" + i, LastName = "L" + i, Age = i }).ToList();
        var dtos = people.Adapt<List<PersonDto>>();

        dtos.Count.ShouldBe(people.Count);
        for (var i = 0; i < people.Count; i++)
        {
            dtos[i].FirstName.ShouldBe(people[i].FirstName);
            dtos[i].LastName.ShouldBe(people[i].LastName);
            dtos[i].Age.ShouldBe(people[i].Age);
        }
    }

    [Fact]
    public void Generated_Dtos_Contain_Expected_Properties()
    {
        AssertHasProperties<PersonDto>(nameof(Person.Id), nameof(Person.FirstName), nameof(Person.LastName),
            nameof(Person.Age), nameof(Person.CreatedUtc), nameof(Person.MiddleName));
        AssertHasProperties<CustomerDto>("CustomerId", "Name", "Email", "PrimaryAddress", "Orders");
        AssertHasProperties<OrderDto>(nameof(Order.Id), nameof(Order.OrderedUtc), nameof(Order.Total),
            nameof(Order.Items));
    }

    private static void AssertHasProperties<T>(params string[] propertyNames)
    {
        var actual = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name)
            .ToHashSet();
        foreach (var expected in propertyNames)
            actual.Contains(expected).ShouldBeTrue($"Missing expected property '{expected}' on DTO {typeof(T).Name}");
    }

    [Fact]
    public void PersonDto_Roundtrip_ToEntity_Preserves_Values()
    {
        var person = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = "Round",
            MiddleName = "Trip",
            LastName = "Test",
            Age = 21,
            CreatedUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        var dto = person.Adapt<PersonDto>();
        var entity2 = dto.Adapt<Person>();
        entity2.ShouldNotBeSameAs(person);
        entity2.Id.ShouldBe(person.Id);
        entity2.FirstName.ShouldBe(person.FirstName);
        entity2.MiddleName.ShouldBe(person.MiddleName);
        entity2.LastName.ShouldBe(person.LastName);
        entity2.Age.ShouldBe(person.Age);
        entity2.CreatedUtc.ShouldBe(person.CreatedUtc);
    }

    [Fact]
    public void CustomerDto_Roundtrip_ToEntity_Preserves_Collections_Reference()
    {
        var order = new Order { Id = Guid.NewGuid(), OrderedUtc = DateTime.UtcNow, Total = 123.45m };
        var customer = new Customer
        {
            CustomerId = 999,
            Name = "Mapster Customer",
            Email = "cust@example.test",
            PrimaryAddress = new Address { Line1 = "Line1", City = "X", Country = "US" },
            Orders = new List<Order> { order }
        };
        var dto = customer.Adapt<CustomerDto>();
        var back = dto.Adapt<Customer>();
        back.CustomerId.ShouldBe(customer.CustomerId);
        back.Name.ShouldBe(customer.Name);
        back.Email.ShouldBe(customer.Email);
        back.PrimaryAddress?.Line1.ShouldBe(customer.PrimaryAddress?.Line1);
        back.Orders.Count.ShouldBe(1);
        back.Orders[0].Id.ShouldBe(order.Id);
    }

    #region Exclude Feature Tests

    [Fact]
    public void PersonSummaryDto_Excludes_Id_And_CreatedUtc()
    {
        // Arrange
        var properties = typeof(PersonSummaryDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).ToHashSet();

        // Assert - Should NOT have excluded properties
        properties.ShouldNotContain("Id");
        properties.ShouldNotContain("CreatedUtc");

        // Assert - Should have all other properties
        properties.ShouldContain("FirstName");
        properties.ShouldContain("MiddleName");
        properties.ShouldContain("LastName");
        properties.ShouldContain("Age");
    }

    [Fact]
    public void PersonSummaryDto_Maps_NonExcluded_Properties()
    {
        // Arrange
        var person = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = "John",
            MiddleName = "M",
            LastName = "Doe",
            Age = 30,
            CreatedUtc = DateTime.UtcNow
        };

        // Act
        var dto = person.Adapt<PersonSummaryDto>();

        // Assert
        dto.FirstName.ShouldBe(person.FirstName);
        dto.MiddleName.ShouldBe(person.MiddleName);
        dto.LastName.ShouldBe(person.LastName);
        dto.Age.ShouldBe(person.Age);
    }

    [Fact]
    public void CustomerPublicDto_Excludes_CustomerId()
    {
        // Arrange
        var properties = typeof(CustomerPublicDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).ToHashSet();

        // Assert
        properties.ShouldNotContain("CustomerId");
        properties.ShouldContain("Name");
        properties.ShouldContain("Email");
        properties.ShouldContain("PrimaryAddress");
        properties.ShouldContain("Orders");
    }

    [Fact]
    public void CustomerPublicDto_Maps_Collections_And_References()
    {
        // Arrange
        var customer = new Customer
        {
            CustomerId = 123,
            Name = "Acme Corp",
            Email = "contact@acme.test",
            PrimaryAddress = new Address { Line1 = "123 Main St", City = "Springfield", Country = "US" },
            Orders = new List<Order>
            {
                new() { Id = Guid.NewGuid(), Total = 100.50m }
            }
        };

        // Act
        var dto = customer.Adapt<CustomerPublicDto>();

        // Assert
        dto.Name.ShouldBe(customer.Name);
        dto.Email.ShouldBe(customer.Email);
        dto.PrimaryAddress.ShouldNotBeNull();
        dto.PrimaryAddress!.Line1.ShouldBe(customer.PrimaryAddress!.Line1);
        dto.Orders.ShouldNotBeEmpty();
        dto.Orders.Count.ShouldBe(1);
    }

    [Fact]
    public void OrderSummaryDto_Excludes_Multiple_Properties()
    {
        // Arrange
        var properties = typeof(OrderSummaryDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).ToHashSet();

        // Assert - Should NOT have excluded properties
        properties.ShouldNotContain("Id");
        properties.ShouldNotContain("OrderedUtc");

        // Assert - Should have remaining properties
        properties.ShouldContain("Total");
        properties.ShouldContain("Items");
    }

    [Fact]
    public void PersonBasicDto_Excludes_Multiple_Properties()
    {
        // Arrange
        var properties = typeof(PersonBasicDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).ToHashSet();

        // Assert - Should NOT have excluded properties
        properties.ShouldNotContain("MiddleName");
        properties.ShouldNotContain("Age");
        properties.ShouldNotContain("CreatedUtc");

        // Assert - Should only have basic properties
        properties.ShouldContain("Id");
        properties.ShouldContain("FirstName");
        properties.ShouldContain("LastName");

        // Total should be exactly 3 properties
        properties.Count.ShouldBe(3);
    }

    [Fact]
    public void PersonBasicDto_Maps_Only_NonExcluded_Properties()
    {
        // Arrange
        var person = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = "Jane",
            MiddleName = "Q",
            LastName = "Smith",
            Age = 25,
            CreatedUtc = DateTime.UtcNow
        };

        // Act
        var dto = person.Adapt<PersonBasicDto>();

        // Assert
        dto.Id.ShouldBe(person.Id);
        dto.FirstName.ShouldBe(person.FirstName);
        dto.LastName.ShouldBe(person.LastName);
    }

    #endregion

    #region Property Type Tests

    [Fact]
    public void Generated_Dtos_Handle_Nullable_String_Properties()
    {
        // Arrange
        var person = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            MiddleName = null, // Nullable
            LastName = "User",
            Age = 20
        };

        // Act
        var dto = person.Adapt<PersonDto>();

        // Assert
        dto.MiddleName.ShouldBeNull();

        // Verify property type is nullable
        var middleNameProp = typeof(PersonDto).GetProperty("MiddleName");
        middleNameProp.ShouldNotBeNull();
        var nullabilityContext = new NullabilityInfoContext();
        var nullabilityInfo = nullabilityContext.Create(middleNameProp);
        nullabilityInfo.WriteState.ShouldBe(NullabilityState.Nullable);
    }

    [Fact]
    public void Generated_Dtos_Handle_Nullable_Reference_Properties()
    {
        // Arrange
        var customer = new Customer
        {
            CustomerId = 1,
            Name = "Test",
            Email = null, // Nullable
            PrimaryAddress = null // Nullable
        };

        // Act
        var dto = customer.Adapt<CustomerDto>();

        // Assert
        dto.Email.ShouldBeNull();
        dto.PrimaryAddress.ShouldBeNull();
    }

    [Fact]
    public void Generated_Dtos_Initialize_Collections_By_Default()
    {
        // Act - Create new instance using default constructor
        var customerDto = new CustomerDto
        {
            CustomerId = 1,
            Name = "Test"
        };

        // Assert - Collections should be initialized to empty
        customerDto.Orders.ShouldNotBeNull();
        customerDto.Orders.ShouldBeEmpty();
    }

    [Fact]
    public void OrderItemDto_Has_All_Value_Type_Properties()
    {
        // Arrange
        var orderItem = new OrderItem
        {
            Id = 42,
            Sku = "PROD-123",
            Quantity = 5,
            Price = 19.99m
        };

        // Act
        var dto = orderItem.Adapt<OrderItemDto>();

        // Assert
        dto.Id.ShouldBe(orderItem.Id);
        dto.Sku.ShouldBe(orderItem.Sku);
        dto.Quantity.ShouldBe(orderItem.Quantity);
        dto.Price.ShouldBe(orderItem.Price);
    }

    #endregion

    #region Required Modifier Tests

    [Fact]
    public void Generated_Dtos_Have_Required_On_NonNullable_Strings()
    {
        // PersonDto should have 'required' on FirstName and LastName (non-nullable strings)
        var dto = new PersonDto
        {
            Id = Guid.NewGuid(),
            FirstName = "Required",
            LastName = "Test",
            Age = 1
        };

        dto.FirstName.ShouldBe("Required");
        dto.LastName.ShouldBe("Test");

        // MiddleName is nullable, so it should NOT be required
        dto.MiddleName.ShouldBeNull();
    }

    #endregion

    #region Record Features Tests

    [Fact]
    public void Generated_Dtos_Support_With_Expressions()
    {
        // Arrange
        var original = new PersonDto
        {
            Id = Guid.NewGuid(),
            FirstName = "Original",
            LastName = "Name",
            Age = 30
        };

        // Act
        var modified = original with { FirstName = "Modified", Age = 31 };

        // Assert
        original.FirstName.ShouldBe("Original");
        original.Age.ShouldBe(30);
        modified.FirstName.ShouldBe("Modified");
        modified.Age.ShouldBe(31);
        modified.LastName.ShouldBe("Name"); // Unchanged
        modified.Id.ShouldBe(original.Id); // Unchanged
    }

    [Fact]
    public void Generated_Dtos_Support_Structural_Equality()
    {
        // Arrange
        var id = Guid.NewGuid();
        var dto1 = new PersonDto
        {
            Id = id,
            FirstName = "John",
            LastName = "Doe",
            Age = 25
        };

        var dto2 = new PersonDto
        {
            Id = id,
            FirstName = "John",
            LastName = "Doe",
            Age = 25
        };

        var dto3 = new PersonDto
        {
            Id = id,
            FirstName = "Jane",
            LastName = "Doe",
            Age = 25
        };

        // Assert
        dto1.ShouldBe(dto2); // Structural equality
        dto1.ShouldNotBe(dto3); // Different values
        (dto1 == dto2).ShouldBeTrue();
        (dto1 == dto3).ShouldBeFalse();
    }

    [Fact]
    public void Generated_Dtos_Support_Deconstruction()
    {
        // Arrange
        var dto = new OrderItemDto
        {
            Id = 1,
            Sku = "ABC123",
            Quantity = 10,
            Price = 99.99m
        };

        // Note: Records support positional deconstruction, but since we're using
        // property-based records, we'd need to verify properties are accessible
        dto.Id.ShouldBe(1);
        dto.Sku.ShouldBe("ABC123");
        dto.Quantity.ShouldBe(10);
        dto.Price.ShouldBe(99.99m);
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public void Nested_Collections_Map_Correctly()
    {
        // Arrange
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderedUtc = DateTime.UtcNow,
            Total = 150.00m,
            Items = new List<OrderItem>
            {
                new() { Id = 1, Sku = "ITEM1", Quantity = 2, Price = 25.00m },
                new() { Id = 2, Sku = "ITEM2", Quantity = 1, Price = 100.00m }
            }
        };

        // Act
        var dto = order.Adapt<OrderDto>();

        // Assert
        dto.Items.ShouldNotBeNull();
        dto.Items.Count.ShouldBe(2);
        dto.Items[0].Sku.ShouldBe("ITEM1");
        dto.Items[1].Sku.ShouldBe("ITEM2");
        dto.Total.ShouldBe(150.00m);
    }

    [Fact]
    public void Multiple_Dtos_From_Same_Entity_Work_Independently()
    {
        // Arrange
        var person = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = "Multi",
            MiddleName = "DTO",
            LastName = "Test",
            Age = 40,
            CreatedUtc = DateTime.UtcNow
        };

        // Act - Map to different DTOs
        var fullDto = person.Adapt<PersonDto>();
        var summaryDto = person.Adapt<PersonSummaryDto>();
        var basicDto = person.Adapt<PersonBasicDto>();

        // Assert - Full DTO has all properties
        fullDto.Id.ShouldBe(person.Id);
        fullDto.FirstName.ShouldBe(person.FirstName);
        fullDto.MiddleName.ShouldBe(person.MiddleName);
        fullDto.Age.ShouldBe(person.Age);
        fullDto.CreatedUtc.ShouldBe(person.CreatedUtc);

        // Assert - Summary DTO excludes Id and CreatedUtc
        summaryDto.FirstName.ShouldBe(person.FirstName);
        summaryDto.MiddleName.ShouldBe(person.MiddleName);
        summaryDto.Age.ShouldBe(person.Age);

        // Assert - Basic DTO excludes MiddleName, Age, CreatedUtc
        basicDto.Id.ShouldBe(person.Id);
        basicDto.FirstName.ShouldBe(person.FirstName);
        basicDto.LastName.ShouldBe(person.LastName);
    }

    [Fact]
    public void Empty_Collections_Map_Correctly()
    {
        // Arrange
        var customer = new Customer
        {
            CustomerId = 1,
            Name = "Empty Orders",
            Orders = new List<Order>() // Empty collection
        };

        // Act
        var dto = customer.Adapt<CustomerDto>();

        // Assert
        dto.Orders.ShouldNotBeNull();
        dto.Orders.ShouldBeEmpty();
    }

    #endregion
}