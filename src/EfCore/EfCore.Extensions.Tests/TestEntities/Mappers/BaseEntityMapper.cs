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

internal sealed class NotInheritIEntityConfig : IEntityTypeConfiguration<NotInheritIEntity>
{
    public void Configure(EntityTypeBuilder<NotInheritIEntity> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
    }
}

/// <summary>
/// EntityTypeConfiguration for the <see cref="User"/> entity.
/// </summary>
internal sealed class UserEntityConfig : IEntityTypeConfiguration<User>
{
    /// <summary>
    /// Configures the <see cref="User"/> entity properties and constraints.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity type.</param>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100);
        builder.Property(x => x.LastName).HasMaxLength(100);
        builder.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedOnAddOrUpdate();
        // Add more configuration as needed (e.g., relationships, default values)
        builder.HasMany(x => x.Addresses).WithOne(x => x.User).HasForeignKey(x => x.UserId);
    }
}

internal sealed class GuidEntityConfig : IEntityTypeConfiguration<GuidEntity>
{
    public void Configure(EntityTypeBuilder<GuidEntity> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
    }
}

internal sealed class GuidAuditEntityConfig : IEntityTypeConfiguration<GuidAuditEntity>
{
    public void Configure(EntityTypeBuilder<GuidAuditEntity> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
    }
}