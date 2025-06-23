using DKNet.EfCore.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKNet.EfCore.Extensions.Configurations;

public class DefaultEntityTypeConfiguration<TEntity> : IEntityTypeConfiguration<TEntity> where TEntity : class
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        if (typeof(IConcurrencyEntity).IsAssignableFrom(builder.Metadata.ClrType))
            builder.Property(nameof(IConcurrencyEntity.RowVersion)).IsRowVersion();
    }
}