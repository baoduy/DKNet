namespace DKNet.EfCore.DtoEntities;

public sealed class Customer : EntityBase
{
    public Customer()
    {
        Name = string.Empty;
        Orders = new List<Order>();
    }

    public int CustomerId { get; set; }
    public string Name { get; set; }
    public string? Email { get; set; }
    public Address? PrimaryAddress { get; set; }
    public List<Order> Orders { get; set; }
}
