# Generate strings and mutable character buffers

Part of [DKNet.RandomCreator](index.md). Use `RandomCreators.NewString` or `NewChars` with `StringCreatorOptions` to choose the output form and character counts.

```csharp
using System;
using DKNet.RandomCreator;

var options = new StringCreatorOptions { MinNumbers = 4, MinSpecials = 2 };
string text = RandomCreators.NewString(32, options);
char[] buffer = RandomCreators.NewChars(32, options);
Console.WriteLine($"{text.Length}, {buffer.Length}"); // 32, 32
Array.Clear(buffer);
```

![NewChars returns StringCreator.ToChars directly, while NewString copies the generated character array into a string.](diagrams/random-output.svg)

Both methods create a fresh `StringCreator` per call. Positive quotas draw exactly that many digits and symbols, the remaining positions draw letters, and `RandomNumberGenerator.Shuffle` reorders the buffer. Each selection uses `RandomNumberGenerator.GetItems<char>`.

| Pool | Characters |
|---|---|
| Letters | `a-z` and `A-Z` (52) |
| Digits | `1234567890` (10) |
| Symbols | ``!@#$%^&*()-_=+[]{}\|;:',.<>/?`~`` (30) |

The character pools are fixed. With default options, all characters are letters; there is no `AlphabeticOnly` flag.

## ⚙️ Configuration reference

| Option | Type | Default | Effect |
|---|---|---|---|
| `length` | `int` | `25` | Total character count; must be positive. |
| `options` | `StringCreatorOptions?` | `null` | Null creates fresh options with zero quotas. |
| `MinNumbers` | `int` | `0` | Exact digit count for a nonnegative quota. |
| `MinSpecials` | `int` | `0` | Exact symbol count for a nonnegative quota. |

## ⚠️ Gotchas & limits

- `length <= 0` throws `ArgumentException`. A quota sum greater than or equal to `length` also throws; nonnegative quotas must leave at least 1 letter.
- Supply nonnegative quotas. The code does not validate each quota separately: negative values skip their draw, and offsetting a positive quota can lead to span-range exceptions.
- There is no seed or uniqueness check. Output varies, and repeated values remain possible.
- Clearing the returned `char[]` changes that array only. `NewString` creates an immutable string and an intermediate character array that the caller cannot clear.
- Symbols can require encoding for their destination. The API does not enforce a particular password policy, case mix or transport alphabet.
