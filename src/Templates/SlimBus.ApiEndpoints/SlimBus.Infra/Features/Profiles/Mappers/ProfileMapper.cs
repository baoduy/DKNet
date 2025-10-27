using SlimBus.Domains.Features.Profiles.Entities;

namespace SlimBus.Infra.Features.Profiles.Mappers;

internal sealed class ProfileMapper : DefaultEntityTypeConfiguration<CustomerProfile>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        base.Configure(builder);

        builder.HasIndex(p => p.Email).IsUnique();
        builder.HasIndex(p => p.MembershipNo).IsUnique();
        builder.Property(p => p.Avatar).HasMaxLength(50);
        builder.Property(p => p.BirthDay).HasColumnType("Date");
        builder.Property(p => p.Email).HasMaxLength(150).IsRequired();
        builder.Property(p => p.MembershipNo).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Phone).HasMaxLength(50).IsRequired(false);
        builder.ToTable("CustomerProfiles", DomainSchemas.Profile);
    }

    #endregion
}