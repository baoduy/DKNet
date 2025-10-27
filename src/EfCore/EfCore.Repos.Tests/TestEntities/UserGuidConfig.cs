using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.Repos.Tests.TestEntities;

internal sealed class UserGuidConfig : DefaultEntityTypeConfiguration<UserGuid>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<UserGuid> builder)
    {
        base.Configure(builder);

        builder.Navigation(u => u.Addresses)
            .HasField("_addresses");

        // SQLite doesn't support automatic row versioning like PostgreSQL or SQL Server
        // Use a simple byte array concurrency token
        builder.Property(u => u.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasMany(u => u.Addresses)
            .WithOne(a => a.User)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    #endregion
}