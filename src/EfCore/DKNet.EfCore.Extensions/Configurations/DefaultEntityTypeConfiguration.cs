using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Convertors;
using DKNet.Fw.Extensions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKNet.EfCore.Extensions.Configurations;

public abstract class DefaultEntityTypeConfiguration<TEntity> : IEntityTypeConfiguration<TEntity> where TEntity : class
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        var clrType = builder.Metadata.ClrType;

        const string idKey = nameof(IEntity<dynamic>.Id);
        // Handle IEntity<T> to set the primary key
        var idProperty = clrType.GetProperty(idKey);
        if (idProperty != null)
        {
            builder.HasKey(idKey);

            if (idProperty.PropertyType.IsNumericType())
                builder.Property(idKey)
                    .ValueGeneratedOnAdd();
            else if (idProperty.PropertyType == typeof(Guid))
                builder.Property(idKey)
                    .ValueGeneratedOnAdd()
                    .HasValueGenerator<GuidV7ValueGenerator>();
        }

        // Handle audit properties
        if (clrType.IsImplementOf<IAuditedProperties>())
        {
            builder.Property(nameof(IAuditedProperties.CreatedBy))
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(nameof(IAuditedProperties.CreatedOn))
                .IsRequired();

            builder.Property(nameof(IAuditedProperties.UpdatedBy))
                .HasMaxLength(255);

            builder.Property(nameof(IAuditedProperties.UpdatedOn));
        }

        if (clrType.IsImplementOf(typeof(IConcurrencyEntity<>)))
            builder.Property(nameof(IConcurrencyEntity<dynamic>.RowVersion))
                .IsConcurrencyToken()
                .IsRowVersion()
                .ValueGeneratedOnAddOrUpdate();
    }
}