# Inspect nullable and generic type shapes

Part of [DKNet.Fw.Extensions](index.md). Use `TypeExtensions`, `PropertyExtensions.IsNullableType` and `StringExtensions.IsStringOrValueType` to classify runtime types.

```csharp
using System;
using System.Collections.Generic;
using DKNet.Fw.Extensions.Reflection;
using DKNet.Fw.Extensions.Primitives;

Console.WriteLine(typeof(List<int>).IsImplementOf(typeof(IEnumerable<>))); // True
Console.WriteLine(typeof(int?).GetNonNullableType() == typeof(int)); // True
Console.WriteLine(typeof(decimal?).IsNumericType()); // True
Console.WriteLine(typeof(string).IsStringOrValueType()); // True
```

![IsImplementOf rejects null and identity before checking assignability, generic interfaces and the base-type chain.](diagrams/type-shape.svg)

`IsAssignableFrom<T>()` and `IsAssignableTo<T>()` wrap the corresponding `Type` operations. `IsImplementOf(Type?)` and `IsImplementOf<T>()` additionally match open generic interfaces and base definitions.

`GetNonNullableType()` unwraps `Nullable<T>` or returns the original type. `IsNullableType()` only recognizes `Nullable<T>`, and `IsEnumType()` also recognizes nullable enums. `IsNumericType()` accepts integer, floating-point and decimal type codes after unwrapping; the object overload returns false for null. `IsStringOrValueType()` accepts a nullable `Type` or `PropertyInfo` and returns false for null.

## ⚠️ Gotchas & limits

- `IsImplementOf` returns false for the same type on both sides, and for either null argument.
- `IsNullableType` and the `Type` overload of `IsNumericType` reject null; null-safe methods do not imply all helpers are null-safe.
- Numeric classification uses `Type.GetTypeCode`, so enums with an integer underlying type also classify as numeric. This does not validate a value or parse text.
- A reference type remains a reference type even when annotated with nullable reference syntax.
