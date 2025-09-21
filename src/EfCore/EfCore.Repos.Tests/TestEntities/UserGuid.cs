using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace EfCore.Repos.Tests.TestEntities;

public class UserGuid : AuditedEntity<Guid>, IConcurrencyEntity<uint>
{
    private readonly HashSet<AddressGuid> _addresses = [];

    public UserGuid(string createdBy) : this(Guid.NewGuid(), createdBy)
    {
    }

    public UserGuid(Guid id, string createdBy) : base(id)
    {
        SetCreatedBy(createdBy);
    }

    [Required] [MaxLength(256)] public required string FirstName { get; set; }

    [Required] [MaxLength(256)] public required string LastName { get; set; }

    [BackingField(nameof(_addresses))] public IReadOnlyCollection<AddressGuid> Addresses => _addresses;

    public string FullName => $"{FirstName} {LastName}";

    public void AddAddress(AddressGuid address) => _addresses.Add(address);

    public uint RowVersion { get; private set; }

    public void SetRowVersion(uint rowVersion)
    {
        RowVersion = rowVersion;
    }
}