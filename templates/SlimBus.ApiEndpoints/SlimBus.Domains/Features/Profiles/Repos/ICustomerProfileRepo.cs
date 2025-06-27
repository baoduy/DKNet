using SlimBus.Domains.Features.Profiles.Entities;
using DKNet.EfCore.Repos.Abstractions;

namespace SlimBus.Domains.Features.Profiles.Repos;

public interface ICustomerProfileRepo : IRepository<CustomerProfile>
{
    Task<bool> IsEmailExistAsync(string email);
}