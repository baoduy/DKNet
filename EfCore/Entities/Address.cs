using System.ComponentModel.DataAnnotations.Schema;

namespace EfCore.TestDataLayer;

public sealed class Address : Entity<int>
{
    public OwnedEntity OwnedEntity { get; set; } = new();
    [Required] [MaxLength(256)] public string Street { get; set; }

    public User User { get; set; }
    [ForeignKey("Address_User")] public long UserId { get; set; }
}