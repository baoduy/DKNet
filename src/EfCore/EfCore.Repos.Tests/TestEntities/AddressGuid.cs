using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.Repos.Tests.TestEntities;

public class AddressGuid : Entity<Guid>
{
    #region Properties

    [MaxLength(100)] public required string City { get; set; }
    [MaxLength(100)] public required string Country { get; set; }

    [MaxLength(100)] public required string Street { get; set; }

    [JsonIgnore] public UserGuid User { get; set; } = null!;
    public Guid UserId { get; set; }

    #endregion
}