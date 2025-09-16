using SlimBus.Domains.Features.Profiles.Entities;

namespace SlimBus.Infra.Contexts;

internal class CoreDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<CustomerProfile> CustomerProfiles { get; set; }
}