using DKNet.EfCore.Repos.Abstractions;
using SlimBus.Domains.Features.Profiles.Entities;

namespace SlimBus.Domains.Features.Profiles.Repos;

public interface ICustomerProfileRepo : IRepository<CustomerProfile>
{
    #region Methods

    Task<bool> IsEmailExistAsync(string email);

    #endregion
}