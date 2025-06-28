using System.ComponentModel.DataAnnotations.Schema;

namespace EfCore.TestDataLayer;

public sealed class Address : Entity<int>
{
    public Address(OwnedEntity ownedEntity)
    {
        OwnedEntity = ownedEntity;
    }

    private Address(int id, OwnedEntity ownedEntity) : base(id)
    {
        OwnedEntity = ownedEntity;
    }

    public OwnedEntity OwnedEntity { get; set; }
    [Required] [MaxLength(256)] public string Street { get; set; } = null!;

    public User User { get; set; } = null!;
    [ForeignKey("Address_User")] public long UserId { get; set; }
}