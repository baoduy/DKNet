using DKNet.SlimBus.Extensions;
using FluentResults;

namespace SlimBus.Extensions.Tests.Data;

public class TestRequest : Fluents.Requests.IWitResponse<Guid>
{
    #region Properties

    public string Name { get; set; } = null!;

    #endregion
}

internal sealed class TestRequestHandler(TestDbContext dbContext) : Fluents.Requests.IHandler<TestRequest, Guid>
{
    #region Methods

    public async Task<IResult<Guid>> OnHandle(TestRequest request, CancellationToken cancellationToken)
    {
        var entity = new TestEntity { Name = request.Name };
        await dbContext.AddAsync(entity, cancellationToken);

        return Result.Ok(entity.Id);
    }

    #endregion
}
