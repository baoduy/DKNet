using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.Extensions.Tests.TestEntities;

public abstract class BaseEntity : AuditedEntity<int>
{
    /// <inheritdoc />
    protected BaseEntity(string createdBy) : this(0, createdBy)
    {
    }

    /// <inheritdoc />
    protected BaseEntity(int id, string createdBy) : base(id, createdBy)
    {
    }

    /// <inheritdoc />
    protected BaseEntity()
    {
    }
}

public class User : BaseEntity
{
    public void UpdatedByUser(string userName) => SetUpdatedBy(userName);

    public User(string createdBy) : this(0, createdBy)
    {
    }

    public User(int id, string createdBy) : base(id, createdBy)
    {
    }

    public Account? Account { get; set; }

    [Required][MaxLength(256)] public required string FirstName { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    [Required][MaxLength(256)] public required string LastName { get; set; }
}

public class Account : Entity<int>
{
    public string Password { get; set; } = null!;
    public string UserName { get; set; } = null!;
}