namespace SlimBus.Extensions.Tests.Data;

public class TestRequest : IRequest<Guid>
{
    #region Properties

    public string Name { get; set; } = null!;

    #endregion
}

internal sealed class TestRequestHandler(TestDbContext dbContext) : IRequestHandler<TestRequest, Guid>
{
    #region Methods

    public async Task<Guid> OnHandle(TestRequest request, CancellationToken cancellationToken)
    {
        var entity = new TestEntity { Name = request.Name };
        await dbContext.AddAsync(entity, cancellationToken);

        return entity.Id;
    }

    #endregion
}