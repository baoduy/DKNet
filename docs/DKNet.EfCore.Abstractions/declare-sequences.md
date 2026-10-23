# Describe database sequences with enum metadata

Part of [DKNet.EfCore.Abstractions](index.md). Put `[SqlSequence]` on an enum and `[Sequence]` on each member that represents a database sequence.

Use the console-project setup in the [Quick Start](index.md#-quick-start), then replace `Program.cs` with this example.

```csharp
using DKNet.EfCore.Abstractions.Attributes;

var field = typeof(InvoiceSequences).GetField(nameof(InvoiceSequences.Number))!;
var rule = (SequenceAttribute)Attribute.GetCustomAttribute(field, typeof(SequenceAttribute))!;
Console.WriteLine(rule.StartAt); // 1000

[SqlSequence("billing")]
public enum InvoiceSequences
{
    [Sequence(typeof(long), StartAt = 1000, FormatString = "INV-{1:000000}")]
    Number
}
```

![SqlSequence supplies a schema and Sequence supplies per-enum-member settings consumed during relational model configuration.](diagrams/declare-sequences.svg)

The metadata declares no database objects itself. [Extensions](../DKNet.EfCore.Extensions/read-sequences.md) registers `Seq_<Member>` on SQL Server or Npgsql models and reads the next value.

## ⚙️ Configuration reference

| Option | Type | Default | Effect |
|---|---|---|---|
| `SqlSequence.Schema` | `string` | `"seq"` | Schema for the enum. |
| `Sequence.Type` | `Type` | `typeof(int)` | Numeric sequence type. |
| `StartAt`, `Min`, `Max` | `long` | `-1` | Apply positive values; otherwise keep database defaults. |
| `IncrementsBy` | `int` | `-1` | Apply a positive step. |
| `Cyclic` | `bool` | `true` | Configure wraparound. |
| `FormatString` | `string?` | `null` | `{1}` is the number; `DateTime` is rewritten to argument `{0}` using UTC and invariant formatting. |

## 🧱 Where it fits

The domain carries enum metadata. Extensions reads that metadata during supported-provider model building and uses it again when the host asks for a sequence value.

![The package component map shows the host, contracts and consumers involved in this capability.](diagrams/components.svg)

## ⚠️ Gotchas & limits

- `[Sequence]` targets fields, not entity properties. Enum members lacking it are skipped during model registration.
- Only `byte`, `short`, `int` and `long` are accepted; another type throws `NotSupportedException` when the attribute is constructed.
- Numeric settings are applied only when greater than zero. Zero and negative increments cannot be expressed through this registration.
