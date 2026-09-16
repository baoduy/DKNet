using DKNet.SlimBus.Extensions;
using FluentResults;

namespace SlimBus.Extensions.Tests.Data;

public class SecondTestRequest : Fluents.Requests.INoResponse
{
    #region Properties

    public string Name { get; set; } = null!;

    #endregion
}

internal sealed class SecondTestRequestHandler(SecondTestDbContext dbContext)
    : Fluents.Requests.IHandler<SecondTestRequest>
{
    #region Methods

    public async Task<IResultBase> OnHandle(SecondTestRequest request, CancellationToken cancellationToken)
    {
        var entity = new SecondTestEntity { Name = request.Name };
        await dbContext.AddAsync(entity, cancellationToken);

        return Result.Ok();
    }

    #endregion
}
