using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.Repos.Tests.TestEntities;

public class Address : Entity<int>
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [MaxLength(100)] public required string Street { get; set; }

    [MaxLength(100)] public required string City { get; set; }

    [MaxLength(100)] public required string Country { get; set; }
}

internal sealed class AddressEfConfig : DefaultEntityTypeConfiguration<Address>
{
    public override void Configure(EntityTypeBuilder<Address> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Street).HasMaxLength(100);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.Country).HasMaxLength(100);
    }
}