using SlimBus.Domains.Features.Profiles.Entities;
using SlimBus.Domains.Features.Profiles.Repos;
using DKNet.EfCore.Repos;

namespace SlimBus.Infra.Features.Profiles.Repos;

internal sealed class CustomerProfileRepo(CoreDbContext dbContext) : Repository<CustomerProfile>(dbContext), ICustomerProfileRepo
{
    public Task<bool> IsEmailExistAsync(string email) => Gets().AnyAsync(f => f.Email == email);
}