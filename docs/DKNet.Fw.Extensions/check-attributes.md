# Check attributes on types and properties

Part of [DKNet.Fw.Extensions](index.md). Use `AttributeExtensions` to test for attributes without manually handling missing reflection metadata.

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

## ⚠️ Gotchas & limits

- These methods check metadata presence; they do not execute validation attributes.
- Assembly scanning's `HasAttribute` disables inheritance and has no `inherit` option; these helpers default to true.
