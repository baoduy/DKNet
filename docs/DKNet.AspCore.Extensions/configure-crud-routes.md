# Configure generated CRUD routes

Part of [DKNet.AspCore.Extensions](index.md). `CrudMapOptions` configures the endpoints emitted by `DKNet.SlimBus.Generators`. You pass it through a generated `Map{Entity}Crud` callback. This standalone example exercises its guards without generated types:

```csharp
using DKNet.AspCore.Extensions.Endpoints;
var options = new CrudMapOptions().Exclude(CrudOp.Delete).Exclude("UpdateName");
options.ValidateRouteNames("Product", "GetById", "GetList", "Create", "Delete", "UpdateName");
Console.WriteLine(options.IsExcluded(CrudOp.Delete)); // True
Console.WriteLine(options.IsExcluded("UpdateName")); // True
```

![The generated mapper validates configured route names, checks exclusions and applies operation settings before route-name settings.](diagrams/crud-options.svg)

`CrudOp` values are `GetById`, `GetList`, `Create`, `Update`, `Delete`, `Action`. `Exclude(params CrudOp[])` removes an entire operation kind; `Exclude(params string[])` removes named routes. `Configure(CrudOp, Action<RouteHandlerBuilder>)` attaches settings to each route of a kind; the string overload targets one name. Both are additive and chainable. Nothing is excluded or customized initially.

The generator checks operation and name exclusions, validates names before mapping, and calls `Apply` with kind and name. `Apply` runs operation settings first and name settings second, each in registration order. Names are ordinal and case-sensitive: fixed operation names plus each marked update/action method’s C# name, not its kebab-case URL segment. Unknown configured/excluded names throw `ArgumentException` from `ValidateRouteNames`.

The generator owns route production and the `[CrudCreate]`, `[CrudUpdate]` and `[CrudAction]` conventions. Read [generator naming and routing](../DKNet.SlimBus.Generators/name-requests-and-routes.md) for those rules.

## ⚠️ Gotchas & limits

- Excluding one named update does not move another update to a new URL.
- Configuring a route whose kind is excluded still validates its name, then has no route to apply to.
- `ValidateRouteNames` and `Apply` are public support methods for generated code; declaring options alone maps nothing.
- This package does not run the source generator or supply business handlers. Install/configure the generator separately.
