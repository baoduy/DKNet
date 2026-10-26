# Mark domain members for CRUD generation

Part of [DKNet.EfCore.Abstractions](index.md). Declare which constructors and methods SlimBus.Generators can turn into request handlers and endpoints.

Use the console-project setup in the [Quick Start](index.md#-quick-start), then replace `Program.cs` with this example.

```csharp
using DKNet.EfCore.Abstractions.Attributes;

Console.WriteLine(new CrudActionAttribute().Verb); // Post

public sealed class Order
{
    [CrudCreate]
    public Order(string customer) => Customer = customer;
    public string Customer { get; private set; }
    [CrudUpdate]
    public void Rename(string customer) => Customer = customer;
    [CrudAction("approve", Verb = CrudActionVerb.Patch, Name = "ApproveOrder")]
    public void Approve() { }
}
```

![CrudCreate marks a creation member while CrudUpdate and CrudAction mark methods that the SlimBus generator translates into generated requests and routes.](diagrams/mark-crud-members.svg)

These declarations are inert without [SlimBus.Generators](../DKNet.SlimBus.Generators/index.md). Keep domain methods as the source of business behavior; the generator owns the transport adapter.

## ⚙️ Configuration reference

| Marker | Targets | Option | Default | Effect |
|---|---|---|---|---|
| `CrudCreate` | Constructor, Method | `Name` | `null` | Override generated Create request name. |
| `CrudUpdate` | Method | `Name` | `null` | Override generated Update request name. |
| `CrudAction` | Method | `route` | `null` | Use generator-derived kebab-case method route. |
| `CrudAction` | Method | `Verb` | `Post` | Select Post, Put or Patch. |
| `CrudAction` | Method | `Name` | `null` | Override generated action request name. |

## 🧱 Where it fits

The domain references only the marker types here. SlimBus.Generators consumes those markers during compilation; generated adapters execute the domain methods in the API host.

![The package component map shows the host, contracts and consumers involved in this capability.](diagrams/components.svg)

## ⚠️ Gotchas & limits

- An action supports Post, Put or Patch; `CrudActionVerb` has no Delete value.
- Do not combine `[CrudUpdate]` and `[CrudAction]` on one member. Consult the generator guide for supported signatures and diagnostics.
