namespace DKNet.EfCore.DtoGenerator.Tests;

public class Person
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public int Age { get; set; }
}

public class Customer
{
    public int CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public Address? PrimaryAddress { get; set; }
    public List<Order> Orders { get; set; } = [];
}

public class Address
{
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class Order
{
    public Guid Id { get; set; }
    public DateTime OrderedUtc { get; set; } = DateTime.UtcNow;
    public decimal Total { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

[GenerateDto(typeof(Person))]
public partial record PersonDto;

[GenerateDto(typeof(Customer))]
public partial record CustomerDto;

[GenerateDto(typeof(Order))]
public partial record OrderDto;

[GenerateDto(typeof(OrderItem))]
public partial record OrderItemDto;

[GenerateDto(typeof(Person))]
public partial record PersonCustomDto
{
    // We intentionally provide our own FirstName; generator should skip generating a second one
    public string FirstName { get; init; } = string.Empty;

    // Add an extra property not present on entity to confirm it stays
    public string DisplayName => FirstName + "?"; // expression-bodied, not init-only; still fine
}

// Test DTOs with Exclude feature
[GenerateDto(typeof(Person), Exclude = ["Id", "CreatedUtc"])]
public partial record PersonSummaryDto;

[GenerateDto(typeof(Customer), Exclude = ["CustomerId"])]
public partial record CustomerPublicDto;

[GenerateDto(typeof(Order), Exclude = ["Id", "OrderedUtc"])]
public partial record OrderSummaryDto;

[GenerateDto(typeof(Person), Exclude = ["MiddleName", "Age", "CreatedUtc"])]
public partial record PersonBasicDto;