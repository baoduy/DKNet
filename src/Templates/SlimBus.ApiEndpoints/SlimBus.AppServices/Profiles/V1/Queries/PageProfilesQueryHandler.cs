using System.Diagnostics.CodeAnalysis;
using DKNet.EfCore.Repos.Abstractions;
using SlimBus.Domains.Features.Profiles.Entities;
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
    IReadRepository<CustomerProfile> repo,
    IMapper mapper) : Fluents.Queries.IPageHandler<PageProfilePageQuery, ProfileResult>
{
    public async Task<IPagedList<ProfileResult>> OnHandle(PageProfilePageQuery request,
        CancellationToken cancellationToken)
    {
        return await repo.Gets()
            .ProjectToType<ProfileResult>(mapper.Config)
            .OrderBy(p => p.Name)
            .ToPagedListAsync(request.PageIndex, request.PageSize, null, cancellationToken);
    }
}