# Read enum display metadata

Part of [DKNet.Fw.Extensions](index.md). Use `EnumExtensions` and `EnumInfo` to build display labels from enum fields.

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using DKNet.Fw.Extensions.Enums;

Console.WriteLine(State.Ready.GetAttribute<DisplayAttribute>()?.Name); // Ready to run
foreach (var info in EnumExtensions.GetEnumInfos<State>())
    Console.WriteLine($"{info.Key}: {info.Name}");
Console.WriteLine(State.Ready.GetEnumInfo()?.Description); // Can start

public enum State
{
    Unknown,
    [Display(Name = "Ready to run", Description = "Can start", GroupName = "Active")]
    Ready
}
```

![GetEnumInfos reads public static enum fields, retrieves DisplayAttribute, and falls back to the field name when Display.Name is absent.](diagrams/enum-metadata.svg)

`EnumInfo` is a record with required `Key` and `Name`, plus nullable `Description` and `GroupName`. `GetEnumInfos<T>()` enumerates public static fields, using their names as keys. The instance `GetAttribute<T>()` looks up a field by the value's text; `GetEnumInfo()` uses that attribute's name, description and group without a name fallback.

## ⚠️ Gotchas & limits

- `GetEnumInfo()` can return an `EnumInfo` whose `Name` is null despite its non-nullable declaration. Only `GetEnumInfos<T>()` supplies a field-name fallback.
- A null `Enum` receiver returns null from the instance helpers. An unnamed value or combined flags value may have no matching field or attribute.
- The helpers read `DisplayAttribute` properties directly; they do not invoke its resource lookup methods.
