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

    [CrudUpdate]
    public void UpdatePrice([Range(0, 1_000_000)] decimal price) => Price = price;
}

/// <summary>Domain event raised when a <see cref="Gadget" /> is created.</summary>
public sealed record GadgetCreated(Guid Id);
