namespace DKNet.EfCore.DtoEntities;

public sealed class Order : EntityBase
{
    public Order()
    {
        OrderNumber = string.Empty;
        Items = [];
    }

    public int OrderId { get; set; }
    public string OrderNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderItem> Items { get; set; }
}

public sealed class OrderItem
{
    public OrderItem() => ProductName = string.Empty;

    public int OrderItemId { get; set; }
    public string ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}