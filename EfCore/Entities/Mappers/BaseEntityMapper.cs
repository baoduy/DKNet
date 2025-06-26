using Microsoft.EntityFrameworkCore;

namespace EfCore.TestDataLayer.Mappers;

internal class BaseEntityMapper<T> : DefaultEntityTypeConfiguration<T> where T : BaseEntity
{
    public static bool Called { get; private set; }

    public override void Configure(EntityTypeBuilder<T> builder)
    {
        Called = true;

        base.Configure(builder);
        builder.HasIndex(c => c.Id).IsUnique();
        builder.Property(c => c.Id).UseIdentityColumn().ValueGeneratedOnAdd();
    }
}