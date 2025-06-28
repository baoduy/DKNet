using System.ComponentModel.DataAnnotations.Schema;

namespace EfCore.TestDataLayer;

public class Payment() : Entity<string>(Guid.NewGuid().ToString())
{
    public DateTime PaidOn { get; } = DateTime.Now;

    [Column(TypeName = "Money")] public decimal Amount { get; set; }
}