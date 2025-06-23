namespace SlimBus.Extensions.Tests.Data;

public class TestRequest : IRequest<Guid>
{
    public string Name { get; set; } = null!;
}

internal sealed class TestRequestHandler(TestDbContext dbContext) : IRequestHandler<TestRequest, Guid>
{
    public async Task<Guid> OnHandle(TestRequest request,CancellationToken cancellationToken)
    {
        var entity = new TestEntity { Name = request.Name };
        await dbContext.AddAsync(entity, cancellationToken);

        return entity.Id;
    }
}