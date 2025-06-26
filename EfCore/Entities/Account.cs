namespace EfCore.TestDataLayer;

public class Account : AuditedEntity<int>
{
    [Required] public string Password { get; set; }
    [Required] public string UserName { get; set; }
}