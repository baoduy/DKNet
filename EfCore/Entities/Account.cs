namespace EfCore.TestDataLayer;

public class Account : Entity<int>
{
    public string Password { get; set; } = null!;
    public string UserName { get; set; } = null!;
}