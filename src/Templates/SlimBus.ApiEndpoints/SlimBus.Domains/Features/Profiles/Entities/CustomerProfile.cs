using System.ComponentModel.DataAnnotations.Schema;
using SlimBus.Domains.Share;

namespace SlimBus.Domains.Features.Profiles.Entities;

[Table("CustomerProfiles", Schema = DomainSchemas.Profile)]
public class CustomerProfile : AggregateRoot
{
    #region Constructors

    public CustomerProfile(string name, string membershipNo, string email, string phone,
        string byUser)
        : this(Guid.Empty, name, membershipNo, email, phone, byUser)
    {
        Name = name;
        Email = email;
        MembershipNo = membershipNo;
    }

    internal CustomerProfile(Guid id, string name, string membershipNo, string email,
        string phone,
        string createdBy)
        : base(id, createdBy)
    {
        Name = name;
        Email = email;
        MembershipNo = membershipNo;

        Update(null, name, phone, null, createdBy);
    }

    #endregion

    #region Properties

    public string? Avatar { get; private set; }

    public DateTime? BirthDay { get; private set; }

    public string Email { get; private set; }

    public string MembershipNo { get; private set; }

    public string Name { get; private set; }

    public string? Phone { get; private set; }

    #endregion

    #region Methods

    public void Update(string? avatar, string? name, string? phoneNumber, DateTime? birthday, string userId)
    {
        Avatar = avatar;
        BirthDay = birthday;

        if (!string.IsNullOrEmpty(name))
            Name = name;
        if (!string.IsNullOrEmpty(phoneNumber))
            Phone = phoneNumber;

        SetUpdatedBy(userId);
    }

    #endregion
}