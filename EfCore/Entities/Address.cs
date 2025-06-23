using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.TestDataLayer;

public class Address() : Entity<int>(0)
{
    #region Public Constructors

    #endregion Public Constructors

    #region Public Properties

    public OwnedEntity OwnedEntity { get; set; } = new();
    [Required] [MaxLength(256)] public string Street { get; set; }

    public virtual User User { get; set; }
    [ForeignKey("Address_User")] public long UserId { get; set; }

    #endregion Public Properties
}