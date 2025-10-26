using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.HookTests.Data;

public class CustomerProfile : IEntity<Guid>
{
    [Required] public string Name { get; set; } = string.Empty;

    public Guid Id { get; set; } = Guid.Empty;
}

internal sealed class CustomerProfileEfConfig : DefaultEntityTypeConfiguration<CustomerProfile>
{
    public override void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
    }
}