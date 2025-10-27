namespace DKNet.EfCore.DtoEntities;

public sealed class Address
{
    #region Constructors

    public Address()
    {
        Street = string.Empty;
        City = string.Empty;
        State = string.Empty;
        ZipCode = string.Empty;
    }

    #endregion

    #region Properties

    public string City { get; set; }
    public string State { get; set; }

    public string Street { get; set; }
    public string ZipCode { get; set; }

    #endregion
}