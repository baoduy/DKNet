namespace EfCore.TestDataLayer;

public class AccountStatus : Entity<int>
{
    [Required] [MaxLength(100)] public string Name { get; set; }= null!;
        
    public AccountStatus()
    {
    }

    public AccountStatus(int id) : base(id)
    {
    }
}