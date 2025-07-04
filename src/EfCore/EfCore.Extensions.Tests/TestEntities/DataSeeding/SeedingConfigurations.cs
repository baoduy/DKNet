using System.Diagnostics.CodeAnalysis;
using DKNet.EfCore.Extensions.Configurations;

namespace EfCore.Extensions.Tests.TestEntities.DataSeeding;

[ExcludeFromCodeCoverage]
public class AccountStatusData : IDataSeedingConfiguration<AccountStatus>
{
    public ICollection<AccountStatus> Data =>
    [
        new(1)
        {
            Name = "Duy",
        },
        new(2)
        {
            Name = "Hoang"
        },
    ];
}

[ExcludeFromCodeCoverage]
public class UserSeedingConfiguration : IDataSeedingConfiguration<User>
{
    public ICollection<User> Data =>
    [
        new(1, "Duy")
        {
            FirstName = "Duy",
            LastName = "Nguyen"
        },
        new(2,"Hoang")
        {
            FirstName = "Hoang",
            LastName = "Le"
        }
    ];
}