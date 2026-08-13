using DKNet.SlimBus.Extensions;
using X.PagedList;

namespace SlimBus.Extensions.Tests.Data;

public class TestPageQuery : Fluents.Queries.IWitPageResponse<TestQueryResult>
{
    #region Properties

    public Guid Id { get; set; }

    #endregion
}

internal class TestPageQueryHandler(TestDbContext dbContext)
    : Fluents.Queries.IPageHandler<TestPageQuery, TestQueryResult>
{
    #region Methods

    public async Task<IPagedList<TestQueryResult>> OnHandle(TestPageQuery request, CancellationToken cancellationToken)
    {
        var rs = await dbContext.FindAsync<TestEntity>([request.Id], cancellationToken);
        var items = rs is null ? [] : new[] { new TestQueryResult { Id = rs.Id, Name = rs.Name } };

        return new PagedList<TestQueryResult>(items, 1, 10);
    }

    #endregion
}
