using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;

namespace SlimBus.Generators.Tests.Domain.Catalog;

/// <summary>Domain fixture for Task 9's end-to-end generated CRUD slice proof.</summary>
public sealed class Gadget : Entity
{
    private Gadget()
    {
    } // EF

    [CrudCreate]
    public Gadget([Required, MaxLength(100)] string name, decimal price)
    {
        Name = name;
        Price = price;
        AddEvent(new GadgetCreated(Id));
    }

    public string Name { get; private set; } = null!;

    public decimal Price { get; private set; }

    public bool IsApproved { get; private set; }

    public bool IsDiscontinued { get; private set; }

    [CrudUpdate]
    public void UpdatePrice([Range(0, 1_000_000)] decimal price) => Price = price;

    /// <summary>
    ///     A domain action (as opposed to a state-replacing update): published as POST at
    ///     <c>{id}/approve</c>, never at the plain by-id route <see cref="UpdatePrice" /> already claims.
    ///     Proves DRK-861's generated request/handler/endpoint slice end-to-end via <c>GadgetCrudSliceTests</c>.
    /// </summary>
    [CrudAction]
    public void Approve() => IsApproved = true;

    /// <summary>
    ///     A second action whose generated handler is suppressed in <c>SlimBus.Generators.Tests.Api</c> by a
    ///     hand-written <c>IHandler&lt;DiscontinueGadgetRequest, GadgetDto&gt;</c> — proves the hand-written
    ///     handler decides the outcome (spec §3.10) over a real HTTP host, not just at the generator level.
    /// </summary>
    [CrudAction]
    public void Discontinue() => IsDiscontinued = true;
}

/// <summary>Domain event raised when a <see cref="Gadget" /> is created.</summary>
public sealed record GadgetCreated(Guid Id);
