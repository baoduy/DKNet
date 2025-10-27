using Microsoft.EntityFrameworkCore;

namespace SlimBus.AppServices.Profiles.V1.Queries;

public record ProfileQuery : Fluents.Queries.IWitResponse<ProfileResult>
{
    #region Properties

    //[FromRoute]
    public required Guid Id { get; init; }

    #endregion
}

internal sealed class SingleProfileQueryHandler(
    IReadRepository<CustomerProfile> repo)
    : Fluents.Queries.IHandler<ProfileQuery, ProfileResult>
{
    #region Methods

    public async Task<ProfileResult?> OnHandle(ProfileQuery request, CancellationToken cancellationToken)
    {
        return await repo.Query<ProfileResult>(p => p.Id == request.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    #endregion
}