using DKNet.EfCore.Repos;
using SlimBus.Domains.Features.Profiles.Entities;
using SlimBus.Domains.Features.Profiles.Repos;
using SlimBus.Infra.Contexts;

namespace SlimBus.Infra.Features.Profiles.Repos;

internal sealed class CustomerProfileRepo(CoreDbContext dbContext)
    : Repository<CustomerProfile>(dbContext), ICustomerProfileRepo
{
    #region Methods

    public Task<bool> IsEmailExistAsync(string email)
    {
        return this.Query().AnyAsync(f => f.Email == email);
    }

    #endregion
}