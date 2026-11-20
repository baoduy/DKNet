# Convert numeric values into enums

Part of [DKNet.Fw.Extensions](index.md). Use `TryConvertToEnum` when a boxed numeric value must become a runtime or generic enum value.

None — no DI registration needed; `TryConvertToEnum` is a static extension method you call directly.

```csharp
using System;
using DKNet.Fw.Extensions.Reflection;

object raw = 1;
if (raw.TryConvertToEnum<State>(out var state))
    Console.WriteLine(state); // Ready
Console.WriteLine(typeof(State).TryConvertToEnum("bad", out _)); // False

public enum State { Unknown, Ready }
```

![TryConvertToEnum unwraps the enum type, converts the input to its numeric underlying type, and creates an enum or returns false for conversion failures.](diagrams/enum-conversion.svg)

The runtime overload accepts `Type` (including nullable enum types), an input object, and an `out object?`. The generic overload uses `where TEnum : struct, Enum` and returns `out TEnum?`. Conversion uses `InvariantCulture`; `InvalidCastException`, `FormatException` and `OverflowException` become false with a null result.

## ⚙️ Configuration reference

None — `TryConvertToEnum` reads no options and exposes no override point.

## 🧱 Where it fits

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| `DynamicPredicateBuilderExtensions` | [DKNet.EfCore.Specifications](../DKNet.EfCore.Specifications/build-dynamic-predicates.md) | Calls the runtime (`Type`) overload to coerce a caller-supplied filter value into an enum when building a dynamic predicate. |

`DynamicPredicateBuilderExtensions` is internal, so the link above goes to [Build runtime predicates](../DKNet.EfCore.Specifications/build-dynamic-predicates.md), the page documenting that coercion.

**Entry point in your app:** calling `TryConvertToEnum` directly on a boxed value or `Type` in your own code.

## ⚠️ Gotchas & limits

- The runtime overload throws `ArgumentException` when the destination is not an enum.
- Conversion does not require a declared enum member: an underlying value such as 99 can succeed.
- A member name such as `"Ready"` is not parsed as an enum name; numeric strings can convert. Use `Enum.TryParse` for named input.
