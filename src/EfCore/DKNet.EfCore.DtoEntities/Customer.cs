namespace DKNet.EfCore.DtoEntities;

public sealed class Customer : EntityBase
{
    #region Constructors

    public Customer()
    {
        Name = string.Empty;
        Orders = [];
    }

    #endregion

    #region Properties

    public int CustomerId { get; set; }
    public string? Email { get; set; }
    public string Name { get; set; }
    public List<Order> Orders { get; set; }
    public Address? PrimaryAddress { get; set; }

    #endregion
}