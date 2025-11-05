namespace DKNet.EfCore.DtoEntities;

public sealed class Order() : EntityBase("Tester")
{
    #region Properties

    public ICollection<OrderItem> Items { get; private set; } = [];

    public int OrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    #endregion
}

public sealed class OrderItem
{
    #region Properties

    public int OrderItemId { get; set; }

    public decimal Price { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    #endregion
}