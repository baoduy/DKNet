using DKNet.EfCore.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKNet.EfCore.Extensions.Configurations;

public class DefaultEntityTypeConfiguration<TEntity> : IEntityTypeConfiguration<TEntity> where TEntity : class
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        //The IEntity<Guid> is regardless to the generic type. Using Guid here just to get the property name
        if (builder.Metadata.ClrType.GetProperty(nameof(IEntity<Guid>.Id)) is not null)
            builder.HasKey(nameof(IEntity<Guid>.Id));

        //These only for IEntity<int>
        if (builder.Metadata.ClrType.IsAssignableTo(typeof(IEntity<int>)))
        {
            builder.Property(nameof(IEntity<int>.Id)).ValueGeneratedOnAdd();
        }

        if (builder.Metadata.ClrType == typeof(IAuditedProperties))
        {
            builder.Property(nameof(IAuditedProperties.CreatedBy)).IsRequired().HasMaxLength(255);
            builder.Property(nameof(IAuditedProperties.CreatedOn)).IsRequired()
                .HasDefaultValueSql("getdate()");
            builder.Property(nameof(IAuditedProperties.UpdatedBy)).HasMaxLength(255);
        }

        if (typeof(IConcurrencyEntity).IsAssignableFrom(builder.Metadata.ClrType))
        {
            builder.Property(nameof(IConcurrencyEntity.RowVersion))
                .IsRequired()
                .ValueGeneratedOnAddOrUpdate()
                .IsRowVersion()
                .HasColumnType("rowversion");
        }
    }
}