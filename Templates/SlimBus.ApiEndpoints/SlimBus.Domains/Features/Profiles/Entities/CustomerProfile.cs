using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SlimBus.Domains.Share;

namespace SlimBus.Domains.Features.Profiles.Entities;


[Table("CustomerProfiles", Schema = DomainSchemas.Profile)]
public class CustomerProfile : AggregateRoot
{
    public CustomerProfile(string name, string memberShipNo, string email, string phone,
        string userId)
        : this(Guid.Empty, name, memberShipNo, email, phone, userId)
    {
    }

    public CustomerProfile(Guid id, string name, string memberShipNo, string email,
        string phone,
        string userId)
        : base(id, userId)
    {
        Email = email;
        MembershipNo = memberShipNo;
        
        Update(avatar: null, name, phone, birthday: null,userId);
    }

    private CustomerProfile()
    {
    }

    [MaxLength(50)] public string? Avatar { get; private set; }
    
    [Column(TypeName = "Date")] public DateTime? BirthDay { get; private set; }

    [MaxLength(150)]
    [EmailAddress]
    [Required]
    public string Email { get;private set; } = null!;

    [MaxLength(50)] [Required] public string MembershipNo { get; private set; } = null!;

    [MaxLength(150)] [Required] 
    public string Name { get; private set; } = null!;

    [Phone] [MaxLength(50)] public string? Phone { get; private set; }

    public void Update(string? avatar,string? name, string? phoneNumber, DateTime? birthday, string userId)
    {
        Avatar = avatar;
        BirthDay = birthday;

        if (!string.IsNullOrEmpty(name))
            Name = name;
        if (!string.IsNullOrEmpty(phoneNumber))
            Phone = phoneNumber;
        
        SetUpdatedBy(userId);
    }
}