# Check attributes on types and properties

Part of [DKNet.Fw.Extensions](index.md). Use `AttributeExtensions` to test for attributes without manually handling missing reflection metadata.

None — no DI registration needed; `AttributeExtensions` is a static extension method class you call directly.

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using DKNet.Fw.Extensions.Reflection;

var item = new Item();
Console.WriteLine(item.HasAttributeOnProperty<RequiredAttribute>(nameof(Item.Name))); // True
Console.WriteLine(item.GetProperty(nameof(Item.Name)).HasAttribute<RequiredAttribute>()); // True
Console.WriteLine(typeof(Item).HasAttribute<SerializableAttribute>()); // False

public sealed class Item { [Required] public string Name { get; set; } = ""; }
```

![HasAttributeOnProperty resolves the named property through GetProperty, then returns Attribute.IsDefined or false when metadata is missing.](diagrams/attribute-check.svg)

`HasAttribute<TAttribute>` accepts a nullable `Type` or `PropertyInfo`. `HasAttributeOnProperty<TAttribute>` resolves the property with the default lookup from [property access](access-properties.md). All forms return false when their metadata lookup finds nothing.

## ⚙️ Configuration reference

| Option | Type | Default | Effect |
|---|---|---|---|
| `inherit` | `bool` | `true` | Passed to `Attribute.IsDefined` to control inherited attribute lookup. |

## 🧱 Where it fits

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| `AuditLogExtensions` | [DKNet.EfCore.AuditLogs](../DKNet.EfCore.AuditLogs/index.md) | Calls the `Type` and `PropertyInfo` overloads of `HasAttribute<TAttribute>` to read `[IgnoreAuditLog]`, `[AuditLog]` and `[SensitiveData]` off an entity type and its properties when building its audit plan. |
| `SequenceExtensions` | [DKNet.EfCore.Extensions](../DKNet.EfCore.Extensions/index.md) | Calls `HasAttribute<SqlSequenceAttribute>()` on each enum type found by [Find types in loaded assemblies](scan-assemblies.md) to decide which enums need a registered SQL sequence. |

Both consumers are internal, so neither package has a page naming them; the links above go to each package's index.

**Entry point in your app:** calling `AttributeExtensions.HasAttribute`/`HasAttributeOnProperty` directly on a `Type`, `PropertyInfo` or instance in your own code.

## ⚠️ Gotchas & limits

- These methods check metadata presence; they do not execute validation attributes.
- Assembly scanning's `HasAttribute` disables inheritance and has no `inherit` option; these helpers default to true.
