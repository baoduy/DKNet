using System.Diagnostics.CodeAnalysis;
using X.PagedList;
using X.PagedList.EF;

namespace SlimBus.AppServices.Profiles.V1.Queries;

public class PageProfilePageQuery : Fluents.Queries.IWitPageResponse<ProfileResult>
{
    #region Properties

    public int PageIndex { get; init; }

    public int PageSize { get; init; } = 100;

    #endregion
}

[SuppressMessage("ReSharper", "UnusedType.Global")]
internal sealed class ProfilePageableQueryValidator : AbstractValidator<PageProfilePageQuery>
{
    #region Constructors

    public ProfilePageableQueryValidator()
    {
        this.RuleFor(x => x.PageSize).NotNull().InclusiveBetween(1, 1000);
        this.RuleFor(x => x.PageIndex).NotNull().InclusiveBetween(0, 1000);
    }

    #endregion
}

internal sealed class PageProfilesQueryHandler(
    IReadRepository<CustomerProfile> repo,
    IMapper mapper) : Fluents.Queries.IPageHandler<PageProfilePageQuery, ProfileResult>
{
    #region Methods

    public async Task<IPagedList<ProfileResult>> OnHandle(
        PageProfilePageQuery request,
        CancellationToken cancellationToken)
    {
        return await repo.Query()
            .ProjectToType<ProfileResult>(mapper.Config)
            .OrderBy(p => p.Name)
            .ToPagedListAsync(request.PageIndex, request.PageSize, null, cancellationToken);
    }

    #endregion
}