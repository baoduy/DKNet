namespace DKNet.EfCore.DtoEntities;

public sealed class Order : EntityBase
{
    #region Constructors

    public Order()
    {
        OrderNumber = string.Empty;
        Items = [];
    }

    #endregion

    #region Properties

    public ICollection<OrderItem> Items { get; private set; }

    public int OrderId { get; set; }
    public string OrderNumber { get; set; }
    public decimal TotalAmount { get; set; }

    #endregion
}

public sealed class OrderItem
{
    #region Constructors

    public OrderItem() => ProductName = string.Empty;

    #endregion

    #region Properties

    public int OrderItemId { get; set; }
    public decimal Price { get; set; }
    public string ProductName { get; set; }
    public int Quantity { get; set; }

    #endregion
}