using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Abstractions.Attributes;

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

    public ICollection<Address> Addresses { get; private set; } = new HashSet<Address>();

    [Required] [MaxLength(256)] public required string FirstName { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    [Required] [MaxLength(256)] public required string LastName { get; set; }
}

public class Account : Entity<int>
{
    public string Password { get; set; } = null!;
    public string UserName { get; set; } = null!;
}

public sealed class Address : Entity<int>
{
    public Address()
    {
    }

    private Address(int id) : base(id)
    {
    }

    public OwnedEntity? OwnedEntity { get; set; }
    [MaxLength(256)] public required string Street { get; set; } = null!;
    [MaxLength(256)] public required string City { get; set; } = null!;

    public User User { get; set; } = null!;
    [ForeignKey("Address_User")] public long UserId { get; set; }
}

[Owned]
[Table("OwnedEntities")]
public class OwnedEntity
{
    public string FullName => $"{nameof(OwnedEntity)} {Name}";
    public string? Name { get; set; }
}

public class GuidEntity : Entity<Guid>
{
    public string Name { get; set; } = null!;
}

public class GuidAuditEntity : AuditedEntity<Guid>
{
    public GuidAuditEntity() : base(Guid.Empty, "Steven")
    {
    }

    public GuidAuditEntity(Guid id, string createdBy) : base(id, createdBy)
    {
    }

    public string Name { get; set; } = null!;
}

// public enum AccountStatusEnum
// {
//     Active = 1,
//     Inactive = 2
// }

public class AccountStatus : Entity<int>
{
    [Required] [MaxLength(100)] public string Name { get; set; } = null!;

    public AccountStatus()
    {
    }

    public AccountStatus(int id) : base(id)
    {
    }
}

[StaticData(nameof(EnumStatus))]
public enum EnumStatus
{
    UnKnow = 0,
    Active = 1,
    InActive = 2,
}

[StaticData("EnumStatusOther")]
public enum EnumStatus1
{
    [Display(Name = "AA", Description = "BB")]
    UnKnow = 0,
    Active = 1,
    InActive = 2,
}

[SqlSequence]
public enum SequencesTest
{
    Order,

    [Sequence(typeof(short), FormatString = "T{DateTime:yyMMdd}{1:00000}", IncrementsBy = 1, Max = short.MaxValue)]
    Invoice,

    [Sequence(typeof(long), IncrementsBy = 1, Max = long.MaxValue)]
    Payment,
}

[IgnoreEntity]
public class IgnoredAutoMapperEntity : Entity<int>
{
    public string Name { get; set; } = null!;
}