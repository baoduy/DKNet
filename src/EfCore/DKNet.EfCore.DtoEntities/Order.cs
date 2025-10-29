namespace DKNet.EfCore.DtoEntities;

public sealed class Order : EntityBase
{
    #region Constructors

    public Order()
    {
        this.OrderNumber = string.Empty;
        this.Items = [];
    }

    #endregion

    #region Properties

    public decimal TotalAmount { get; set; }

    public ICollection<OrderItem> Items { get; private set; }

    public int OrderId { get; set; }

    public string OrderNumber { get; set; }

    #endregion
}

public sealed class OrderItem
{
    #region Constructors

    public OrderItem() => this.ProductName = string.Empty;

    #endregion

    #region Properties

    public decimal Price { get; set; }

    public int OrderItemId { get; set; }

    public int Quantity { get; set; }

    public string ProductName { get; set; }

    #endregion
}