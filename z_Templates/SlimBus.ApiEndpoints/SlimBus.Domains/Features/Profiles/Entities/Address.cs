using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SlimBus.Domains.Features.Profiles.Entities;

[Owned]
public class Address
{
    public Address(string line, string state, string city, string country, string postal)
    {
        Line = line;
        State = state;
        City = city;
        Country = country;
        Postal = postal;
    }

    internal Address()
    {
    }

    [MaxLength(50), Required] public string City { get; private set; } = null!;

    [MaxLength(50),Required] public string Country { get;private set; } = null!;

    [MaxLength(50),Required] public string Line { get; private set; } = null!;

    [MaxLength(50),Required] public string Postal { get; private set; } = null!;

    [MaxLength(50),Required] public string State { get; private set; } = null!;
}