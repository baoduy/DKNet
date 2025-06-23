using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SlimBus.Domains.Share;

namespace SlimBus.Domains.Features.Profiles.Entities;

public enum EmployeeType
{
    None = 0,
    Director = 1,
    Secretary = 2,
    Other = 3,
}

[Table("Employees", Schema = DomainSchemas.Profile)]
public class Employee : DomainEntity
{
    public Employee(Guid profileId, EmployeeType type, string userId) : base(Guid.NewGuid(), userId)
    {
        ProfileId = profileId;
        PromoteTo(type, userId);
    }

    private Employee()
    {
    }


    [Required] public virtual CustomerProfile Profile { get; private set; } = null!;

    [Required] public Guid ProfileId { get; private set; }

    [Required] public EmployeeType Type { get; private set; }


    public void PromoteTo(EmployeeType type, string userId)
    {
        Type = type;
        SetUpdatedBy(userId);
    }
}