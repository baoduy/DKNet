using SlimBus.Domains.Features.Profiles.Entities;

namespace SlimBus.Infra.Features.Profiles.Mappers;

internal sealed class ProfileMapper : DefaultEntityTypeConfiguration<CustomerProfile>
{
    public override void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        base.Configure(builder);

        builder.HasIndex(p => p.Email).IsUnique();
        builder.HasIndex(p => p.MembershipNo).IsUnique();
    }
}