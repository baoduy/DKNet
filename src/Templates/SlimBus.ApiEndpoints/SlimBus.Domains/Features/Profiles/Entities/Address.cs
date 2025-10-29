using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SlimBus.Domains.Features.Profiles.Entities;

[Owned]
public class Address
{
    #region Constructors

    public Address(string line, string state, string city, string country, string postal)
    {
        this.Line = line;
        this.State = state;
        this.City = city;
        this.Country = country;
        this.Postal = postal;
    }

    internal Address()
    {
    }

    #endregion

    #region Properties

    [MaxLength(50)] [Required] public string City { get; private set; } = null!;

    [MaxLength(50)] [Required] public string Country { get; private set; } = null!;

    [MaxLength(50)] [Required] public string Line { get; private set; } = null!;

    [MaxLength(50)] [Required] public string Postal { get; private set; } = null!;

    [MaxLength(50)] [Required] public string State { get; private set; } = null!;

    #endregion
}