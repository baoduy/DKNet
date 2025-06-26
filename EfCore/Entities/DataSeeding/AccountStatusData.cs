namespace EfCore.TestDataLayer.DataSeeding;

[ExcludeFromCodeCoverage]
public class AccountStatusData : IDataSeedingConfiguration<AccountStatus>
{
    #region Properties

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

    #endregion Properties
}