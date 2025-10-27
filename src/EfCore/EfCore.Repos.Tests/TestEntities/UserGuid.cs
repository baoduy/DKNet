using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.Repos.Tests.TestEntities;

public class UserGuid : AuditedEntity<Guid>, IConcurrencyEntity<uint>
{
    #region Fields

    private readonly HashSet<AddressGuid> _addresses = [];

    #endregion

    #region Constructors

    public UserGuid(string createdBy) : this(Guid.NewGuid(), createdBy)
    {
    }

    public UserGuid(Guid id, string createdBy) : base(id)
    {
        SetCreatedBy(createdBy);
    }

    #endregion

    #region Properties

    [BackingField(nameof(_addresses))] public IReadOnlyCollection<AddressGuid> Addresses => _addresses;

    [Required] [MaxLength(256)] public required string FirstName { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    [Required] [MaxLength(256)] public required string LastName { get; set; }

    public uint RowVersion { get; private set; }

    #endregion

    #region Methods

    public void AddAddress(AddressGuid address) => _addresses.Add(address);

    public void SetRowVersion(uint rowVersion)
    {
        RowVersion = rowVersion;
    }

    #endregion
}