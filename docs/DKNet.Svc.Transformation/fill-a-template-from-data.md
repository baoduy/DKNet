# Fill a template from objects or dictionaries

Part of [DKNet.Svc.Transformation](index.md). `Transform`/`TransformAsync` extract every token in the template, resolve each one against the `parameters` you pass, and rebuild the string in a single pass.

```csharp
var order = new { OrderId = 4711, Total = 129.5m, Placed = new DateTime(2026, 3, 1), Paid = true };
var text = transformer.Transform("Order [OrderId] — [Total] on [Placed]. Paid: [Paid]", order);
// Order 4,711 — 129.50 on 01/03/2026 12.00.00. Paid: Yes
```

Resolution walks the `parameters` array in order, then falls back to `TransformOptions.GlobalParameters` (set at [registration](register-the-service.md)). For each entry: an `IDictionary<string, string>` is looked up by key, case-insensitively, wherever it sits — passed directly, inside a collection at any nesting depth, or in `GlobalParameters`; an `IEnumerable<object>` — including `GlobalParameters` itself — is searched recursively, item by item; anything else is treated as an object — a public instance property matching the token name wins, and a non-public one is tried next. An `IDictionary<string, string>` answers by key only, wherever it sits, so `[Count]` or `[Keys]` does not resolve from it; any other dictionary inside a collection or in `GlobalParameters`, such as `Dictionary<string, object>`, is read as an object, so its keys are not used and the next item is tried. The first non-null value wins.

![Each parameter is tried through TokenResolver.Resolve, which picks the lookup — dictionary key, collection search, or property reflection — from the object's runtime type; the first non-null value found wins.](diagrams/resolve-token-value.svg)

This does not show the not-found policy (below) or value formatting, which run after resolution for every token regardless of which source supplied the value.

```csharp
public enum TokenNotFoundBehavior { LeaveAsIs, Remove, ThrowError } // default: ThrowError
```

`ThrowError` (the default) throws `UnResolvedTokenException` on the first token nothing can resolve. `LeaveAsIs` keeps the token text verbatim. `Remove` replaces it with an empty string. A property that exists but holds `null` counts as unresolved — there is no way to distinguish "missing" from "null" here.

## ⚙️ Configuration reference

| Option | Type | Default | Effect |
|---|---|---|---|
| `TokenNotFoundBehavior` | `TokenNotFoundBehavior` | `ThrowError` | `LeaveAsIs` / `Remove` for lenient rendering instead of throwing `UnResolvedTokenException`. |
| `GlobalParameters` | `IEnumerable<object>` | `[]` | Fallback resolution sources tried after the call's own `parameters`. Configured at [registration](register-the-service.md). |

## ⚠️ Gotchas & limits

- **A dictionary passed directly must be `IDictionary<string, string>`.** Any other `IDictionary` passed as a `Transform`/`TransformAsync` argument — including `Dictionary<string, object>` — throws `ArgumentException` during resolution. Inside a collection or in `GlobalParameters` it does not throw: it is read as an object, so its keys never answer a token.
- **No nested-property or dotted paths.** `[Customer.Name]` is looked up as a single property literally named `Customer.Name`, which normally fails; flatten your data (project to an anonymous object or dictionary) first.
- **Non-public properties are readable.** Resolution falls back to non-public instance properties, so a token can pull a value the type does not expose publicly. Don't pass entities with sensitive internal state into a user-authored template.
- **Resolved values are cached only for the current call.** The cache key is the token string, scoped to a single `Transform`/`TransformAsync` call — a token repeated twice in the same template resolves once, but nothing is ever shared across calls or across a reused `ITransformerService` instance.
