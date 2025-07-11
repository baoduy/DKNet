using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.Repos.Tests.TestEntities;

public class AddressGuid : Entity<Guid>
{
    public Guid UserId { get; set; }
    public UserGuid User { get; set; } = null!;

    [MaxLength(100)] public required string Street { get; set; } = string.Empty;
    [MaxLength(100)] public required string City { get; set; } = string.Empty;
    [MaxLength(100)] public required string Country { get; set; } = string.Empty;
}