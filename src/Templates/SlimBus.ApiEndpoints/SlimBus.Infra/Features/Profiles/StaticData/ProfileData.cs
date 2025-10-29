using SlimBus.Domains.Features.Profiles.Entities;

namespace SlimBus.Infra.Features.Profiles.StaticData;

internal sealed class ProfileData : DataSeedingConfiguration<CustomerProfile>
{
    #region Properties

    protected override ICollection<CustomerProfile> HasData =>
    [
        new(
            new Guid("A6B50327-160E-423C-9C0B-C125588E6025"),
            "Steven Hoang",
            "MS12345",
            "abc@gmail.com",
            "123456789",
            SharedConsts.SystemAccount)
    ];

    #endregion
}