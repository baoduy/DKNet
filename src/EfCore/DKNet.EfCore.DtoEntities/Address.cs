namespace DKNet.EfCore.DtoEntities;

public sealed class Address
{
    #region Properties

    public string City { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string Street { get; set; } = string.Empty;

    public string ZipCode { get; set; } = string.Empty;

    #endregion
}