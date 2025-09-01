using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace EfCore.Repos.Tests.TestEntities;

public class User : Entity<int>
{
    private readonly HashSet<Address> _addresses = [];

    [Required] [MaxLength(256)] public required string FirstName { get; set; }

    [Required] [MaxLength(256)] public required string LastName { get; set; }

    [BackingField(nameof(_addresses))] public IReadOnlyCollection<Address> Addresses => _addresses;

    public string FullName => $"{FirstName} {LastName}";

    public void AddAddress(Address address)
    {
        _addresses.Add(address);
    }
}