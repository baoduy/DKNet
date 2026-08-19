namespace EfCore.DtoGenerator.TestEntities;

public sealed class Customer() : EntityBase("Tester")
{
    #region Properties

    public int CustomerId { get; set; }

    public string? Email { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Order> Orders { get; } = [];

    public Address? PrimaryAddress { get; set; }

    #endregion
}