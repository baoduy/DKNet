using Microsoft.EntityFrameworkCore;

namespace EfCore.TestDataLayer.Mappers;

internal class NotInheritIEntityConfig : DefaultEntityTypeConfiguration<NotInheritIEntity>
{
    public override void Configure(EntityTypeBuilder<NotInheritIEntity> builder)
    {
        base.Configure(builder);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasMaxLength(50)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWID()");
    }
}