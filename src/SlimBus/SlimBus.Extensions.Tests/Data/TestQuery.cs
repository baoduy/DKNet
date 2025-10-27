using DKNet.SlimBus.Extensions;

namespace SlimBus.Extensions.Tests.Data;

public class TestQuery : Fluents.Queries.IWitResponse<TestQueryResult>
{
    #region Properties

    public Guid Id { get; set; }

    #endregion
}

public class TestQueryResult
{
    #region Properties

    public Guid Id { get; set; } = Guid.Empty;
    public string Name { get; set; } = null!;

    #endregion
}

internal class TestQueryHandler(TestDbContext dbContext, IMapper mapper)
    : Fluents.Queries.IHandler<TestQuery, TestQueryResult>
{
    #region Methods

    public async Task<TestQueryResult?> OnHandle(TestQuery request, CancellationToken cancellationToken)
    {
        var rs = await dbContext.FindAsync<TestEntity>([request.Id], cancellationToken);
        return rs is null ? null : mapper.Map<TestQueryResult>(rs);
    }

    #endregion
}