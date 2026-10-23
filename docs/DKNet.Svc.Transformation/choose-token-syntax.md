# Choose token syntax

Part of [DKNet.Svc.Transformation](index.md). Any combination of `ITokenDefinition` values can be active at once through `TransformOptions.DefaultDefinitions`, which already contains `SquareBrackets`.

```csharp
services.AddTransformerService(options =>
{
    options.DefaultDefinitions.Add(TransformOptions.CurlyBrackets);
    options.DefaultDefinitions.Add(TransformOptions.AngledBrackets);
});
```

| Definition | Syntax |
|---|---|
| `TransformOptions.SquareBrackets` (default) | `[Token]` |
| `TransformOptions.CurlyBrackets` | `{Token}` |
| `TransformOptions.AngledBrackets` | `<Token>` |
| `TransformOptions.DoubleCurlyBrackets` | {% raw %}`{{Token}}`{% endraw %} |

Define your own pair with `new TokenDefinition(begin, end)` (from `DKNet.Svc.Transformation.TokenExtractors`) — it throws `ArgumentException` for a null or whitespace tag.

![One TokenExtractor runs per registered definition, scanning for its begin/end tags; TokenDefinition.IsToken rejects a candidate with empty inner text or a nested begin tag before the match reaches TokenResolver.](diagrams/choose-token-syntax.svg)

This does not show what happens once a token is matched — see [fill a template from data](fill-a-template-from-data.md) — or that extraction runs once per `Transform`/`TransformAsync` call for each active definition, independently.

## ⚙️ Configuration reference

| Option | Type | Default | Effect |
|---|---|---|---|
| `DefaultDefinitions` | `ICollection<ITokenDefinition>` (get-only) | `[SquareBrackets]` | Bracket styles recognized; add more, cannot be replaced or cleared through the property setter — there is no setter, only the collection's own mutators. |

## 🧱 Where it fits

Token syntax is purely an extraction-time concern: `TransformerService` builds one internal `TokenExtractor` per entry in `DefaultDefinitions` when it is constructed, and every active definition is tried against the full template on every call. See the package [🏗️ Architecture](index.md#-architecture) for where extraction sits between the host call and resolution.

## ⚠️ Gotchas & limits

- **A candidate only counts as a token when its inner text is non-empty and contains no character from either tag.** `[Total (USD)]` resolves but `[a[b]` does not.
- **Overlapping bracket styles are ambiguous.** Enabling both `CurlyBrackets` and `DoubleCurlyBrackets` means the same text can match two definitions and be substituted twice; pick one of the two.
- **`ITokenExtractor` is a public interface with no injection point.** Its implementation, `TokenExtractor`, is `internal sealed`, and `TransformerService` constructs one per definition itself — implementing `ITokenExtractor` compiles but nothing will ever call it. Extend behaviour through `DefaultDefinitions` instead.
