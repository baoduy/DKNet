using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Repos.Abstractions;

namespace SlimBus.AppServices.Profiles.V1.Queries;

public record ProfileQuery : Fluents.Queries.IWitResponse<ProfileResult>
{
    //[FromRoute]
    public required Guid Id { get; init; }
}

internal sealed class SingleProfileQueryHandler(
    IReadRepository<Domains.Features.Profiles.Entities.CustomerProfile> repo)
    : Fluents.Queries.IHandler<ProfileQuery, ProfileResult>
{
    public async Task<ProfileResult?> OnHandle(ProfileQuery request, CancellationToken cancellationToken)
        => await repo.GetProjection<ProfileResult>().FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken: cancellationToken);
}