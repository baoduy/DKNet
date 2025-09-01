using DKNet.EfCore.Abstractions.Entities;
using DKNet.Fw.Extensions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKNet.EfCore.Extensions.Configurations;

public class DefaultEntityTypeConfiguration<TEntity> : IEntityTypeConfiguration<TEntity> where TEntity : class
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        var clrType = builder.Metadata.ClrType;

        // Handle IEntity<T> to set the primary key
        var idProperty = clrType.GetProperty(nameof(IEntity<dynamic>.Id));
        if (idProperty != null)
        {
            builder.HasKey(nameof(IEntity<dynamic>.Id));

            if (idProperty.PropertyType.IsNumericType())
                builder.Property(nameof(IEntity<dynamic>.Id)).ValueGeneratedOnAdd();
        }

        // Handle audit properties
        if (typeof(IAuditedProperties).IsAssignableFrom(clrType))
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
    }
}