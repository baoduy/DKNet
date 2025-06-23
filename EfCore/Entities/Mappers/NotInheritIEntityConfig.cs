using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.TestDataLayer.Mappers;

internal class NotInheritIEntityConfig : DefaultEntityTypeConfiguration<NotInheritIEntity>
{
    public override void Configure(EntityTypeBuilder<NotInheritIEntity> builder)
    {
        base.Configure(builder);
    }
}