using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.Repos.Tests.TestEntities;

public class AddressGuid : Entity<Guid>
{
    public Guid UserId { get; set; }

    [JsonIgnore] public UserGuid User { get; set; } = null!;

    [MaxLength(100)] public required string Street { get; set; }
    [MaxLength(100)] public required string City { get; set; }
    [MaxLength(100)] public required string Country { get; set; }
}