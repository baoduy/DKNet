namespace DKNet.EfCore.DtoEntities;

public sealed class Customer : EntityBase
{
    #region Constructors

    public Customer()
    {
        this.Name = string.Empty;
        this.Orders = [];
    }

    #endregion

    #region Properties

    public Address? PrimaryAddress { get; set; }

    public ICollection<Order> Orders { get; }

    public int CustomerId { get; set; }

    public string Name { get; set; }

    public string? Email { get; set; }

    #endregion
}