using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.Repos.Tests.TestEntities;

public class User : AuditedEntity<int>
{
    #region Fields

    private readonly HashSet<Address> _addresses = [];

    #endregion

    #region Constructors

    public User(string createdBy) : this(0, createdBy)
    {
    }

    public User(int id, string createdBy) : base(id)
    {
        this.SetCreatedBy(createdBy);
    }

    #endregion

    #region Properties

    [BackingField(nameof(_addresses))] public IReadOnlyCollection<Address> Addresses => this._addresses;

    [Required] [MaxLength(256)] public required string FirstName { get; set; }

    public string FullName => $"{this.FirstName} {this.LastName}";

    [Required] [MaxLength(256)] public required string LastName { get; set; }

    #endregion

    #region Methods

    public void AddAddress(Address address)
    {
        this._addresses.Add(address);
    }

    #endregion
}