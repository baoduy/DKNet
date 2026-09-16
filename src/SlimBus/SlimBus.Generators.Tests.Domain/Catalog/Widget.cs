using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;

namespace SlimBus.Generators.Tests.Domain.Catalog;

/// <summary>
///     Second CRUD fixture entity for DRK-1326: proves a delete rule registered against
///     <see cref="Gadget" />'s delete request never fires against <see cref="Widget" />'s own delete request.
///     Carries <see cref="GadgetId" /> so a test fixture validator can decide whether a <see cref="Gadget" />
///     "holds" any accounts.
/// </summary>
public sealed class Widget : Entity
{
    private Widget()
    {
    } // EF

    [CrudCreate]
    public Widget(string name, Guid gadgetId)
    {
        Name = name;
        GadgetId = gadgetId;
    }

    public string Name { get; private set; } = null!;

    public Guid GadgetId { get; private set; }
}
