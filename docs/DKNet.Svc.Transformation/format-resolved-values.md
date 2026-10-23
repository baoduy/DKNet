# Format resolved values

Part of [DKNet.Svc.Transformation](index.md). The default `ValueFormatter` turns a resolved value into its display string before it replaces the token in the template.

```csharp
public sealed class IsoDateFormatter : ValueFormatter
{
    public override string DateFormat { get; set; } = "yyyy-MM-dd";
}

services.AddTransformerService(options => options.Formatter = new IsoDateFormatter());
```

`Convert` receives the `IToken` as well as the value, so a custom formatter can branch on `token.Key` when one template needs a token-specific format.

![A resolved value reaches IValueFormatter.Convert, which switches on its runtime type and renders it with CultureInfo.InvariantCulture into the display text substituted into the template.](diagrams/format-resolved-value.svg)

This does not show what happens before formatting — token extraction and resolution — or the not-found policy, which never reaches `Convert` at all: an unresolved token is handled separately (see [fill a template from data](fill-a-template-from-data.md)).

```csharp
public virtual string DateFormat { get; set; } = "dd/MM/yyyy hh.mm.ss";
public virtual string IntegerFormat { get; set; } = "###,##0";
public virtual string NumberFormat { get; set; } = "###,##0.00";
```

`bool` becomes `"Yes"`/`"No"`; `int`/`long` use `IntegerFormat`; `double`/`decimal`/`float` use `NumberFormat`; `DateTime`/`DateTimeOffset` use `DateFormat`; everything else falls back to `ToString()`. All numeric and date formatting runs against `CultureInfo.InvariantCulture`, so output does not shift with the thread's culture.

## ⚙️ Configuration reference

| Option | Type | Default | Effect |
|---|---|---|---|
| `Formatter` | `IValueFormatter` | `new ValueFormatter()` | Converts each resolved value to its display string. |
| `ValueFormatter.DateFormat` | `string` | `"dd/MM/yyyy hh.mm.ss"` | Format string for `DateTime`/`DateTimeOffset` values. |
| `ValueFormatter.IntegerFormat` | `string` | `"###,##0"` | Format string for `int`/`long` values. |
| `ValueFormatter.NumberFormat` | `string` | `"###,##0.00"` | Format string for `double`/`decimal`/`float` values. |

## ⚠️ Gotchas & limits

- **The default date format is 12-hour with no meridiem.** `"dd/MM/yyyy hh.mm.ss"` renders 13:05 as `01.05.00` — override `DateFormat` (e.g. `"HH:mm"`) for anything a reader must interpret unambiguously.
- **`IValueFormatter`/`ValueFormatter` are the one fully open extension point here.** Subclass `ValueFormatter` to change just the formats, or implement `IValueFormatter` directly for full control (a non-numeric, non-date, non-bool type, or a type-specific format), then assign `TransformOptions.Formatter`.
