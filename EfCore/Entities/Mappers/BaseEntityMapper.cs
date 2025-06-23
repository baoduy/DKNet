using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.TestDataLayer.Mappers;

internal class BaseEntityMapper<T> : DefaultEntityTypeConfiguration<T> where T : BaseEntity
{
    #region Properties

    public static bool Called { get; private set; }

    #endregion Properties

    #region Methods

    public override void Configure(EntityTypeBuilder<T> builder)
    {
        Called = true;

        base.Configure(builder);
        builder.HasIndex(c => c.Id).IsUnique();
        builder.Property(c => c.Id).UseIdentityColumn().ValueGeneratedOnAdd();
    }

    #endregion Methods
}