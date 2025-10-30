using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.Repos.Tests.TestEntities;

public class UserGuid : AuditedEntity<Guid>, IConcurrencyEntity<byte[]>
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
        this.SetCreatedBy(createdBy);
    }

    #endregion

    #region Properties

    public byte[]? RowVersion { get; private set; }

    [BackingField(nameof(_addresses))] public IReadOnlyCollection<AddressGuid> Addresses => this._addresses;

    [Required] [MaxLength(256)] public required string FirstName { get; set; }

    public string FullName => $"{this.FirstName} {this.LastName}";

    [Required] [MaxLength(256)] public required string LastName { get; set; }

    #endregion

    #region Methods

    public void AddAddress(AddressGuid address) => this._addresses.Add(address);

    public void SetRowVersion(byte[] rowVersion)
    {
        this.RowVersion = rowVersion;
    }

    #endregion
}