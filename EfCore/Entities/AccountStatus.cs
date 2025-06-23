using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.TestDataLayer;

public class AccountStatus : Entity<int>
{
    [Required] [MaxLength(100)] public string Name { get; set; }
        
    public AccountStatus()
    {
    }

    public AccountStatus(int id) : base(id)
    {
    }
}