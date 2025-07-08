using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.Repos.Tests.TestEntities;

public class Address:Entity<int>
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [MaxLength(100)]
    public required string Street { get; set; } = string.Empty;
    [MaxLength(100)]
    public required string City { get; set; } = string.Empty;
    [MaxLength(100)]
    public required string Country { get; set; } = string.Empty;
}