using DKNet.SlimBus.Extensions;

namespace SlimBus.Generators.Tests.Api;

/// <summary>
///     Hand-authored stand-in for the delete request DKNet.SlimBus.Generators will emit for
///     <see cref="SlimBus.Generators.Tests.Domain.Catalog.Gadget" /> once DRK-1326's generator change lands —
///     carries the target's key so a delete rule has something to validate against ahead of the generated
///     type existing. Deliberately outside the <c>SlimBus.Generators.Tests.Api.Crud</c> namespace the
///     generator emits into, so it never collides with the real generated type.
/// </summary>
public sealed record DeleteGadgetRequest : Fluents.Requests.IWithKey<Guid>
{
    /// <inheritdoc />
    public Guid Id { get; set; }
}

/// <summary>Hand-authored stand-in for Widget's delete request — see <see cref="DeleteGadgetRequest" />.</summary>
public sealed record DeleteWidgetRequest : Fluents.Requests.IWithKey<Guid>
{
    /// <inheritdoc />
    public Guid Id { get; set; }
}
