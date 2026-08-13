using DKNet.SlimBus.Extensions;
using FluentResults;

namespace SlimBus.Extensions.Tests.Data;

public class TestFailRequest : Fluents.Requests.IWitResponse<Guid>
{
    #region Properties

    public string Name { get; set; } = null!;

    #endregion
}

internal sealed class TestFailRequestHandler(TestDbContext dbContext)
    : Fluents.Requests.IHandler<TestFailRequest, Guid>
{
    #region Methods

    public async Task<IResult<Guid>> OnHandle(TestFailRequest request, CancellationToken cancellationToken)
    {
        var entity = new TestEntity { Name = request.Name };
        await dbContext.AddAsync(entity, cancellationToken);

        return Result.Fail<Guid>("Fail");
    }

    #endregion
}
