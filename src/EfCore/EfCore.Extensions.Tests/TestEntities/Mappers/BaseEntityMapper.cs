using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.Extensions.Tests.TestEntities.Mappers;

internal class BaseEntityMapper<T> : DefaultEntityTypeConfiguration<T> where T : class
{
    public static bool Called { get; private set; }

    public override void Configure(EntityTypeBuilder<T> builder)
    {
        Called = true;
        base.Configure(builder);
    }
}

// Need this for the tests that check for entities that don't inherit IEntity
public class NotInheritIEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

internal class NotInheritIEntityConfig : IEntityTypeConfiguration<NotInheritIEntity>
{
    public void Configure(EntityTypeBuilder<NotInheritIEntity> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
    }
}