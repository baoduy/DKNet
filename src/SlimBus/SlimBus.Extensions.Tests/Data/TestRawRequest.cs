using SlimMessageBus;

namespace SlimBus.Extensions.Tests.Data;

public class TestRawRequest : IRequest<Guid>
{
    #region Properties

    public string Name { get; set; } = null!;

    #endregion
}

internal sealed class TestRawRequestHandler(TestDbContext dbContext) : IRequestHandler<TestRawRequest, Guid>
{
    #region Methods

    public async Task<Guid> OnHandle(TestRawRequest request, CancellationToken cancellationToken)
    {
        var entity = new TestEntity { Name = request.Name };
        await dbContext.AddAsync(entity, cancellationToken);

        return entity.Id;
    }

    #endregion
}
