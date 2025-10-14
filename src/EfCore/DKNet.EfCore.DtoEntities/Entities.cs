namespace DKNet.EfCore.DtoEntities;

public abstract class EntityBase
{
    public DateTime CreatedUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string? UpdatedBy { get; private set; }
}

public class CurrencyData : EntityBase
{
    public int Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }
}

public class Person : EntityBase
{
    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string? MiddleName { get; private set; }
    public string LastName { get; private set; } = string.Empty;
    public int Age { get; private set; }
}

public class Customer : EntityBase
{
    public int CustomerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public Address? PrimaryAddress { get; private set; }
    public List<Order> Orders { get; private set; } = [];
}

public class Address : EntityBase
{
    public string Line1 { get; private set; } = string.Empty;
    public string? Line2 { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
}

public class Order:EntityBase
{
    public Guid Id { get; private set; }
    public DateTime OrderedUtc { get; private set; } = DateTime.UtcNow;
    public decimal Total { get; private set; }
    public List<OrderItem> Items { get; private set; } = [];
}

public class OrderItem
{
    public int Id { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal Price { get; private set; }
}