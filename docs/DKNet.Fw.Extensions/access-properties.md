# Read and update properties by name

Part of [DKNet.Fw.Extensions](index.md). Use `PropertyExtensions` for case-insensitive instance-property lookup, dotted reads and converted writes.

```csharp
using System;
using DKNet.Fw.Extensions.Reflection;

var item = new Item { Owner = new Owner { Name = "Ada" } };
Console.WriteLine(item.GetPropertyValue("Owner.Name")); // Ada
item.SetPropertyValue(nameof(Item.Count), "12");
Console.WriteLine(item.Count); // 12
item.TrySetPropertyValue("Missing", 1); // missing property is ignored

public sealed class Item
{
    public Owner? Owner { get; set; }
    public int Count { get; set; }
}
public sealed class Owner { public string Name { get; set; } = ""; }
```

![SetPropertyValue resolves a property, converts a non-null value using its destination type, and invokes PropertyInfo.SetValue.](diagrams/property-write.svg)

`GetProperty` caches `PropertyInfo` lookups, including missing results, by `(Type, name, flags)` in a static concurrent dictionary. It accepts an object or a `Type` receiver; use `PropertyExtensions.GetProperty(typeof(Item), name)` to avoid the instance `Type.GetProperty` overload.

`GetPropertyValue` walks dotted segments and returns `null` when a segment is missing or its value is null. Writes accept a property name or a `PropertyInfo`; they do not walk dotted paths. A non-null value goes through `Convert.ChangeType` with `CurrentCulture`; non-nullable enum destinations use `Enum.Parse` instead. Nullable destinations convert to their underlying type.

## ⚙️ Configuration reference

| Option | Type | Default | Effect |
|---|---|---|---|
| `GetProperty` `flags` | `BindingFlags` | `IgnoreCase \| Public \| NonPublic \| Instance` | Controls reflection lookup. Dotted reads and named writes use this default. |

## ⚠️ Gotchas & limits

- `SetPropertyValue` throws for a null receiver, null metadata, or a missing named property. Reflection getter/setter exceptions also propagate.
- Both `TrySetPropertyValue` overloads return `void`. Their initial null checks occur before the `try` and still throw.
- The name overload catches `ArgumentNullException` and `ArgumentException` from the write. The metadata overload catches `ArgumentNullException` and `FormatException`. Other failures propagate; neither is a universal exception shield.
- `Convert.ChangeType` does not support every destination type; nullable enum conversion has no enum-specific branch.
- The metadata cache is shared across calls and has no eviction; values themselves are read on each call.
