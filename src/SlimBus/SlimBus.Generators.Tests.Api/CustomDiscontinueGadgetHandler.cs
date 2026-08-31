using DKNet.SlimBus.Extensions;
using FluentResults;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using SlimBus.Generators.Tests.Api.Crud;

namespace SlimBus.Generators.Tests.Api;

/// <summary>
///     Hand-written override for the generated <c>Discontinue</c> action handler — proves a hand-written
///     <c>IHandler&lt;DiscontinueGadgetRequest, GadgetDto&gt;</c> suppresses the generated one (spec §3.10)
///     and decides the outcome itself, exercised over a real HTTP host by <see cref="GadgetCrudSliceTests" />.
///     Deliberately does NOT flip <c>Gadget.IsDiscontinued</c> — the generated handler always would — so a
///     passing assertion that the flag stayed <see langword="false" /> after a successful call is only
///     explainable by THIS handler having run.
/// </summary>
internal sealed class CustomDiscontinueGadgetHandler(GadgetDbContext db, IMapper mapper)
    : Fluents.Requests.IHandler<DiscontinueGadgetRequest, GadgetDto>
{
    public async Task<IResult<GadgetDto>> OnHandle(DiscontinueGadgetRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.Gadgets.FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);
        if (entity is null)
            return Result.Fail<GadgetDto>(new NotFoundError($"Gadget '{request.Id}' was not found (custom handler)."));

        return Result.Ok(mapper.Map<GadgetDto>(entity));
    }
}
