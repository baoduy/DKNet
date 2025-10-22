using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;

namespace DKNet.EfCore.DtoEntities;

/// <summary>
/// Test entity to verify validation attribute copying in DTOs
/// </summary>
public sealed class TestProduct : IEntity<Guid>
{
    public TestProduct(string name, string sku, decimal price, int stockQuantity, string email)
    {
        Id = Guid.NewGuid();
        Name = name;
        Sku = sku;
        Price = price;
        StockQuantity = stockQuantity;
        Email = email;
        CreatedDate = DateTime.UtcNow;
    }

    private TestProduct()
    {
        Id = Guid.Empty;
        Name = string.Empty;
        Sku = string.Empty;
        Email = string.Empty;
        CreatedDate = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; private set; }

    [Required]
    [MaxLength(50)]
    public string Sku { get; private set; }

    [Range(0.01, 999999.99)]
    public decimal Price { get; private set; }

    [Range(0, 10000)]
    public int StockQuantity { get; private set; }

    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; private set; }

    [MaxLength(500)]
    public string? Description { get; private set; }

    [Url]
    public string? WebsiteUrl { get; private set; }

    [Phone]
    public string? PhoneNumber { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public bool IsActive { get; private set; } = true;
}
