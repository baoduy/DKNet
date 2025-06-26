using System.ComponentModel.DataAnnotations.Schema;

namespace EfCore.TestDataLayer;

public class User : BaseEntity
{

    public void UpdatedByUser(string userName) => SetUpdatedBy(userName);

    public User(string userName) : this(0, userName)
    {
    }

    public User(int id, string userName) : base(id, userName)
    {
    }

    public User()
    {
    }


    public Account Account { get; set; }

    /// <summary>
    ///     Private Set for Data Seeding purpose.
    /// </summary>
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    public ICollection<Address> Addresses { get; private set; } = new HashSet<Address>();

    [Required][MaxLength(256)] public required string FirstName { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    [Required][MaxLength(256)] public required string LastName { get; set; }

    public ICollection<Payment> Payments { get; } = new HashSet<Payment>();

    // ReSharper disable once UnusedAutoPropertyAccessor.Local
    [Column(TypeName = "Money")] public decimal TotalPayment { get; private set; }

    // ReSharper disable once UnusedAutoPropertyAccessor.Local
    [NotMapped] public bool TotalPaymentCalculated { get; private set; }
}