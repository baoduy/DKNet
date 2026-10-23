# Build runtime predicates

Part of [DKNet.EfCore.Specifications](index.md). For caller-supplied criteria, validate the triple before using it. In the Quick Start, add `using LinqKit;` and `using DKNet.EfCore.Specifications.Dynamics;`, then query:

```csharp
if (!DynamicPredicateExtensions.TryBuildPredicate<Product>("price", Ops.GreaterThanOrEqual, "10", out var filter))
    throw new ArgumentException("Invalid price filter.");
var matches = await db.Products.AsExpandable().Where(filter!).ToListAsync();
Console.WriteLine(matches.Single().Name); // Coffee
```

![TryBuildPredicate validates an operation and normalized property path, coerces values, and returns a parsed predicate or false before the caller chooses whether to accept the filter.](diagrams/build-dynamic-predicates.svg)

`DynamicAnd`/`DynamicOr` in namespace `LinqKit` accept either `ExpressionStarter<T>` or `Expression<Func<T, bool>>`. Their typed `(propertyName, Ops, value)` overloads return the predicate unchanged for unusable conditions. Use `TryBuildPredicate` at a request boundary so invalid conditions cannot silently widen the result set.

| Ops | Behavior |
|---|---|
| Equal / NotEqual | Equality; a null value produces a null comparison. |
| GreaterThan / GreaterThanOrEqual / LessThan / LessThanOrEqual | Ordered comparison. |
| Contains / NotContains / StartsWith / EndsWith | String operations; non-string targets adjust to equality/inequality. |
| In / NotIn | Non-empty collection membership, excluding a scalar string. |
| IsNull / IsNotNull | Null test; supplied value is ignored, and unsupported non-nullable targets fail validation. |

Paths normalize camelCase, snake_case and kebab-case segments to PascalCase, including dotted paths. Scalar conversion supports numeric types, booleans, date/time types, Guid and enums. String collections for In/NotIn are converted element by element; already-typed collections must match the target, and invalid enum values are rejected. Typed filter values are parameterized for EF query-plan reuse.

## 🧱 Where it fits

![A validated predicate enters a specification or an expandable DbSet query, then EF composes it into SQL when materialized.](diagrams/predicate-components.svg)

Repository queries already call `AsExpandable()`. Raw `DbSet`/`IQueryable` paths must call it themselves when composing LinqKit expressions.

## ⚠️ Gotchas & limits

- Invalid operations, unsafe/unknown paths, failed coercion and parse failures yield false from TryBuildPredicate or silently skip the typed DynamicAnd/DynamicOr condition. Neither is automatic request validation.
- Do not skip null values indiscriminately: Equal/NotEqual and IsNull/IsNotNull have meaningful null behavior.
- The raw `(string expression, params object?[] values)` overload uses a blocklist and can throw ArgumentException or Dynamic LINQ parse exceptions. Keep expression structure under application control; a blocklist is not an authorization policy.
- Validate allowed fields in the host as well as type validity. Successful parsing does not grant permission to filter on a sensitive field.
