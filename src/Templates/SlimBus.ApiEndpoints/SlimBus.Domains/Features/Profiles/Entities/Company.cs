using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SlimBus.Domains.Features.Profiles.Entities;

[Owned]
public class Company
{
    #region Constructors

    public Company(string name, string uen, string abn, string arbn, string can)
    {
        this.Name = name;
        this.UEN = uen;
        this.ABN = abn;
        this.ARBN = arbn;
        this.CAN = can;
    }

    internal Company()
    {
    }

    #endregion

    #region Properties

    [MaxLength(100)] [Required] public string Name { get; private set; } = null!;

    [MaxLength(100)] [Required] public string UEN { get; private set; } = null!;

    [MaxLength(50)] public string? ABN { get; private set; }

    [MaxLength(50)] public string? ARBN { get; private set; }

    [MaxLength(50)] public string? CAN { get; private set; }

    #endregion
}