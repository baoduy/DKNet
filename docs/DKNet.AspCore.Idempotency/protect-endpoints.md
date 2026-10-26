# Protect endpoints and route groups

Part of [DKNet.AspCore.Idempotency](index.md). Register services as in the overview, then declare protection on a route or group:

```csharp
using DKNet.AspCore.Idempotency;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotentKey();
var app = builder.Build();
var orders = app.MapGroup("/orders").RequiredIdempotentKey();
orders.MapPost("/", () => Results.Ok());
orders.MapGet("/", () => Results.Ok());
app.MapPost("/single", () => Results.Ok()).RequiredIdempotentKey();
await app.RunAsync();
```

![The group convention matches routed verbs, attaches the filter to covered endpoints, and leaves unmatched endpoints unchanged.](diagrams/protect-groups.svg)

The group overload defaults to POST. Pass explicit verbs, such as `RequiredIdempotentKey("POST", "DELETE")`, to cover others. Matching uses routed HTTP method metadata, case-insensitively, at endpoint build time; request method-override headers do not change coverage. Nested groups inherit the convention, including endpoints registered before the declaration. A per-request marker prevents double execution when both declarations apply.

## ⚙️ Configuration reference

| Input | Type | Default | Effect |
|---|---|---|---|
| `httpMethods` | `params string[]` | POST | Selects the routed verbs covered by a group. |

Header and key settings are owned by [key validation](validate-keys.md).

## ⚠️ Gotchas & limits

- A verb-less `Map(...)` endpoint has no method metadata to match and remains unprotected by a group declaration.
- Unmatched endpoints do no key validation, lookup, or retention. PUT and DELETE require explicit inclusion.
- Protection is for minimal APIs; there is no MVC action attribute.
- Store lookup failures propagate. The filter does not invoke a handler when reservation fails.
