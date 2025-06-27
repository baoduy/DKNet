using System.Diagnostics.CodeAnalysis;
using Mapster;
using DKNet.EfCore.Repos.Abstractions;
using X.PagedList;
using X.PagedList.EF;

namespace SlimBus.AppServices.Profiles.V1.Queries;

public class PageProfilePageQuery : Fluents.Queries.IWitPageResponse<ProfileResult>
{
    public int PageSize { get; init; } = 100;
    public int PageIndex { get; init; }
}

[SuppressMessage("ReSharper", "UnusedType.Global")]
internal sealed class ProfilePageableQueryValidator : AbstractValidator<PageProfilePageQuery>
{
    public ProfilePageableQueryValidator()
    {
        RuleFor(x => x.PageSize).NotNull().InclusiveBetween(1, 1000);
        RuleFor(x => x.PageIndex).NotNull().InclusiveBetween(0, 1000);
    }
}

internal sealed class PageProfilesQueryHandler(
    IReadRepository<Domains.Features.Profiles.Entities.CustomerProfile> repo,
    IMapper mapper) : Fluents.Queries.IPageHandler<PageProfilePageQuery, ProfileResult>
{
    public async Task<IPagedList<ProfileResult>> OnHandle(PageProfilePageQuery request, CancellationToken cancellationToken) =>
        await repo.Gets()
            .ProjectToType<ProfileResult>(mapper.Config)
            .OrderBy(p => p.Name)
            .ToPagedListAsync(pageNumber: request.PageIndex, pageSize: request.PageSize, totalSetCount: null, cancellationToken: cancellationToken);
}