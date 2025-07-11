using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace EfCore.Repos.Tests.TestEntities;

public class User : AuditedEntity<int>
{
    private readonly HashSet<Address> _addresses = [];

    public User(string createdBy) : this(0, createdBy)
    {
    }

    public User(int id, string createdBy) : base(id, createdBy)
    {
    }

    [Required] [MaxLength(256)] public required string FirstName { get; set; }

    [Required] [MaxLength(256)] public required string LastName { get; set; }

    [BackingField(nameof(_addresses))] public IReadOnlyCollection<Address> Addresses => _addresses;

    public string FullName => $"{FirstName} {LastName}";

    public void AddAddress(Address address) => _addresses.Add(address);
}