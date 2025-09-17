using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.Extensions.Tests.TestEntities.Mappers;

internal class AddressEntityMapper : BaseEntityMapper<Address>
{
    public override void Configure(EntityTypeBuilder<Address> builder)
    {
        base.Configure(builder);
        builder.HasIndex(c => c.Id).IsUnique();
    }
}