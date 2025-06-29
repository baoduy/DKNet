using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.Repos.Tests.TestEntities;

public class User : AuditedEntity<int>
{
    public User(string createdBy) : this(0, createdBy)
    {
    }

    public User(int id, string createdBy) : base(id, createdBy)
    {
    }

    [Required][MaxLength(256)] public required string FirstName { get; set; }

    [Required][MaxLength(256)] public required string LastName { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}