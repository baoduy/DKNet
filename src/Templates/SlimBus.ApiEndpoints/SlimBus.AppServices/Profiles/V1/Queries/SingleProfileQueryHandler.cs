using DKNet.EfCore.Repos.Abstractions;
using Microsoft.EntityFrameworkCore;
using SlimBus.Domains.Features.Profiles.Entities;

namespace SlimBus.AppServices.Profiles.V1.Queries;

public record ProfileQuery : Fluents.Queries.IWitResponse<ProfileResult>
{
    //[FromRoute]
    public required Guid Id { get; init; }
}

internal sealed class SingleProfileQueryHandler(
    IReadRepository<CustomerProfile> repo)
    : Fluents.Queries.IHandler<ProfileQuery, ProfileResult>
{
    public async Task<ProfileResult?> OnHandle(ProfileQuery request, CancellationToken cancellationToken)
    {
        return await repo.GetDto<ProfileResult>()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
    }
}