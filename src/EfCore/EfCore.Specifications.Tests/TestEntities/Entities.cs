using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.Specifications.Tests.TestEntities;

public class Product : Entity<int>
{
    public Product()
    {
    }

    public Product(int id) : base(id)
    {
    }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public ICollection<OrderItem> OrderItems { get; set; } = new HashSet<OrderItem>();

    public ICollection<ProductTag> ProductTags { get; set; } = new HashSet<ProductTag>();
}

public class Category : Entity<int>
{
    public Category()
    {
    }

    public Category(int id) : base(id)
    {
    }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public ICollection<Product> Products { get; set; } = new HashSet<Product>();
}

public class Order : Entity<int>
{
    public Order()
    {
    }

    public Order(int id) : base(id)
    {
    }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public decimal TotalAmount { get; set; }

    [MaxLength(100)]
    public string? CustomerName { get; set; }

    [MaxLength(200)]
    public string? CustomerEmail { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public ICollection<OrderItem> OrderItems { get; set; } = new HashSet<OrderItem>();
}

public class OrderItem : Entity<int>
{
    public OrderItem()
    {
    }

    public OrderItem(int id) : base(id)
    {
    }

    public int OrderId { get; set; }

    public Order? Order { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TotalPrice => Quantity * UnitPrice;
}

public class ProductTag : Entity<int>
{
    public ProductTag()
    {
    }

    public ProductTag(int id) : base(id)
    {
    }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    [Required]
    [MaxLength(50)]
    public string Tag { get; set; } = string.Empty;
}

public enum OrderStatus
{
    Pending = 0,
    Processing = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4
}

