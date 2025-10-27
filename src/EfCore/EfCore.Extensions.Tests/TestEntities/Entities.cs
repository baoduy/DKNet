using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.Extensions.Tests.TestEntities;

public abstract class BaseEntity : AuditedEntity<int>, IConcurrencyEntity<byte[]>
{
    #region Constructors

    /// <inheritdoc />
    protected BaseEntity(string createdBy) : this(0, createdBy)
    {
    }

    /// <inheritdoc />
    protected BaseEntity(int id, string createdBy) : base(id)
    {
        SetCreatedBy(createdBy);
    }

    /// <inheritdoc />
    protected BaseEntity()
    {
    }

    #endregion

    #region Properties

    public byte[]? RowVersion { get; private set; }

    #endregion

    #region Methods

    public void SetRowVersion(byte[] rowVersion)
    {
        RowVersion = rowVersion;
    }

    #endregion
}

public class User : BaseEntity
{
    #region Constructors

    public User(string createdBy) : base(createdBy)
    {
    }

    public User(int id, string createdBy) : base(id, createdBy)
    {
    }

    #endregion

    #region Properties

    public ICollection<Address> Addresses { get; private set; } = new HashSet<Address>();

    [Required] [MaxLength(256)] public required string FirstName { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    [Required] [MaxLength(256)] public required string LastName { get; set; }

    #endregion

    #region Methods

    public void UpdatedByUser(string userName) => SetUpdatedBy(userName);

    #endregion
}

public class Account : Entity<int>
{
    #region Constructors

    public Account()
    {
    }

    public Account(int id) : base(id)
    {
    }

    #endregion

    #region Properties

    [MaxLength(500)] public string Password { get; set; } = null!;

    [MaxLength(500)] public string UserName { get; set; } = null!;

    #endregion
}

public sealed class Address : Entity<int>
{
    #region Constructors

    public Address()
    {
    }

    private Address(int id) : base(id)
    {
    }

    #endregion

    #region Properties

    [MaxLength(256)] public required string City { get; set; } = null!;

    public OwnedEntity? OwnedEntity { get; set; }
    [MaxLength(256)] public required string Street { get; set; } = null!;

    public User User { get; set; } = null!;
    [ForeignKey("Address_User")] public int UserId { get; set; }

    #endregion
}

[Owned]
[Table("OwnedEntities")]
public class OwnedEntity
{
    #region Properties

    public string FullName => $"{nameof(OwnedEntity)} {Name}";

    [MaxLength(500)] public string? Name { get; set; }

    #endregion
}

public class GuidEntity : Entity<Guid>
{
    #region Properties

    public string Name { get; set; } = null!;

    #endregion
}

public class GuidAuditEntity : AuditedEntity<Guid>
{
    #region Constructors

    public GuidAuditEntity() : base(default) => SetCreatedBy("Steven");

    public GuidAuditEntity(Guid id, string createdBy) : base(id) => SetCreatedBy(createdBy);

    #endregion

    #region Properties

    public string Name { get; set; } = null!;

    #endregion
}

public class AccountStatus : Entity<int>
{
    #region Constructors

    public AccountStatus()
    {
    }

    public AccountStatus(int id) : base(id)
    {
    }

    #endregion

    #region Properties

    [Required] [MaxLength(100)] public string Name { get; set; } = null!;

    #endregion
}

// [StaticData(nameof(EnumStatus))]
// public enum EnumStatus
// {
//     UnKnow = 0,
//     Active = 1,
//     InActive = 2
// }
//
// [StaticData("EnumStatusOther")]
// public enum EnumStatus1
// {
//     [Display(Name = "AA", Description = "BB")]
//     UnKnow = 0,
//     Active = 1,
//     InActive = 2
// }

[SqlSequence]
public enum SequencesTest
{
    Order,

    [Sequence(typeof(short), FormatString = "T{DateTime:yyMMdd}{1:00000}", IncrementsBy = 1, Max = short.MaxValue)]
    Invoice,

    [Sequence(typeof(long), IncrementsBy = 1, Max = long.MaxValue)]
    Payment
}

[IgnoreEntity]
public class IgnoredAutoMapperEntity : Entity<int>
{
    #region Properties

    public string Name { get; set; } = null!;

    #endregion
}