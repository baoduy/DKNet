# Add a custom contextual source

Part of [DKNet.AspCore.Extensions](index.md). [Populate request members from context](bind-contextual-members.md) ships two sources, `[FromClaim]` and `[FromRequestHeader]`. Implement `IContextualSource` and `IContextualValueResolver` to add a new one — for example, populating a property from the caller's remote IP address.

```csharp
using DKNet.AspCore.Extensions.ModelBinding;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddContextualRequestPopulation();
services.AddScoped<IContextualValueResolver, ClientIpValueResolver>();
Console.WriteLine("Custom IContextualValueResolver registered alongside the built-in claim/header resolvers.");

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FromClientIpAttribute : Attribute, IContextualSource;

public sealed class ClientIpValueResolver : IContextualValueResolver
{
    public bool CanResolve(IContextualSource source) => source is FromClientIpAttribute;

    public string? Resolve(IContextualSource source, HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString();
}

public sealed record AuditedRequest
{
    [FromClientIp]
    public string? ClientIp { get; set; }
}
```

![Workflow diagram of adding a custom contextual source: you write an attribute implementing IContextualSource and a resolver implementing IContextualValueResolver, register the resolver scoped alongside the built-in ones in either order, then the population mechanism picks the first resolver whose CanResolve returns true for a declared member.](diagrams/add-a-contextual-source.svg)

Register your resolver with `services.AddScoped<IContextualValueResolver, TYourResolver>()`, before or after `AddContextualRequestPopulation()` — both calls add to the same `IEnumerable<IContextualValueResolver>`, and [the population mechanism](bind-contextual-members.md) tries them in registration order, so register your resolver first if it must run before a built-in one for the same attribute type. Decorate a request property with your attribute exactly as you would `[FromClaim]` or `[FromRequestHeader]`; no other registration step is needed.

## ⚙️ Configuration reference

| Interface | Member | How you implement it |
|---|---|---|
| `IContextualSource` | *(marker, no members)* | Add it to your own attribute's base-type list: `public sealed class FromXAttribute : Attribute, IContextualSource`. |
| `IContextualValueResolver` | `bool CanResolve(IContextualSource source)` | Return `true` only for the declaration(s) your resolver handles, typically `source is FromXAttribute`. |
| `IContextualValueResolver` | `string? Resolve(IContextualSource source, HttpContext httpContext)` | Return the raw string value, or `null` when none is available; a `null` result still wins over a later resolver and becomes the property's type default. |

There is no options class here: the two interfaces are the override point. Full non-extension behaviour (conversion, defaults, OpenAPI hiding) is documented on [Populate request members from context](bind-contextual-members.md).

## 🧱 Where it fits

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| [`ContextualRequestPopulationService`](bind-contextual-members.md) | DKNet.AspCore.Extensions (same package) | Resolves every registered `IContextualValueResolver` (built-in and yours) through DI and calls `CanResolve` on each, in registration order, for every declared member it populates. |

None outside this package — no other DKNet package implements `IContextualSource`/`IContextualValueResolver` or declares a custom contextual source.

**Entry point in your app:** decorating a request property with your custom attribute, plus `services.AddScoped<IContextualValueResolver, TYourResolver>()` in `Program.cs`.

## ⚠️ Gotchas & limits

- `CanResolve` is checked in registration order, and the first match wins even if it returns a `null` value — a resolver registered after yours for the same attribute type never runs for that declaration.
- `IContextualSource` has no members: your attribute carries whatever data `Resolve` needs (a key name, a format) as its own properties, the same way `FromClaimAttribute.ClaimType` and `FromRequestHeaderAttribute.HeaderName` do.
- Resolvers are resolved scoped, alongside `ContextualRequestPopulationService`. A resolver that needs a scoped dependency (for example a tenant lookup) works correctly; avoid registering one as a singleton that captures a scoped service.
- Your custom source's declared property is still removed from the generated OpenAPI body schema and query parameters — that hiding checks `IContextualSource` generically, not the concrete attribute type. Only the explicit `in: header` parameter the transformer adds for documentation is specific to `[FromRequestHeader]`; a custom source gets none, even if it also reads a header.
