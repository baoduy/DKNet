using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.Repos.Tests.TestEntities;

internal sealed class UserGuidConfig : DefaultEntityTypeConfiguration<UserGuid>
{
    public override void Configure(EntityTypeBuilder<UserGuid> builder)
    {
        base.Configure(builder);

        builder.Navigation(u => u.Addresses)
            .HasField("_addresses");

        builder.Property(u => u.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate()
            .HasColumnType("xid");

        builder.HasMany(u => u.Addresses)
            .WithOne(a => a.User)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}