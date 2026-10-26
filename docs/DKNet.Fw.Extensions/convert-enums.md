# Convert numeric values into enums

Part of [DKNet.Fw.Extensions](index.md). Use `TryConvertToEnum` when a boxed numeric value must become a runtime or generic enum value.

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

## ⚠️ Gotchas & limits

- The runtime overload throws `ArgumentException` when the destination is not an enum.
- Conversion does not require a declared enum member: an underlying value such as 99 can succeed.
- A member name such as `"Ready"` is not parsed as an enum name; numeric strings can convert. Use `Enum.TryParse` for named input.
