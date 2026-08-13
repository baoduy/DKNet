using DKNet.SlimBus.Extensions;
using FluentResults;

namespace SlimBus.Extensions.Tests.Data;

public class TestNoResponseRequest : Fluents.Requests.INoResponse
{
    #region Properties

    public string Name { get; set; } = null!;

    #endregion
}

internal sealed class TestNoResponseRequestHandler(TestDbContext dbContext)
    : Fluents.Requests.IHandler<TestNoResponseRequest>
{
    #region Methods

    public async Task<IResultBase> OnHandle(TestNoResponseRequest request, CancellationToken cancellationToken)
    {
        var entity = new TestEntity { Name = request.Name };
        await dbContext.AddAsync(entity, cancellationToken);

        return Result.Ok();
    }

    #endregion
}
