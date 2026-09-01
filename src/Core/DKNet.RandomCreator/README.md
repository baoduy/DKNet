# DKNet.RandomCreator

A tiny, dependency-free .NET library for generating cryptographically secure random strings and character arrays — suitable for passwords, tokens, and other secrets. Randomness comes from `System.Security.Cryptography.RandomNumberGenerator`, not `System.Random`.

## Installation

```bash
dotnet add package DKNet.RandomCreator
```

## Features

- Random `string` or `char[]` generation of any length (`RandomCreators.NewString` / `RandomCreators.NewChars`), default length 25
- `StringCreatorOptions.MinNumbers` / `MinSpecials` — guarantee exact quotas of digits and symbol characters (great for password-strength rules)
- Letters-only output by simply leaving both quotas at their default of `0`
- Output is shuffled so required digits/symbols are never clumped at a fixed position
- No DKNet or third-party dependencies — just the .NET base class library

## Quick start

```csharp
using DKNet.RandomCreator;

// 25-character letters-only string (defaults)
string token = RandomCreators.NewString();

// 32 characters, at least 4 digits and 2 symbols, rest letters
var options = new StringCreatorOptions { MinNumbers = 4, MinSpecials = 2 };
string strongPassword = RandomCreators.NewString(32, options);
```

## Customisation reference

Both entry points take the same two arguments; every option lives on `StringCreatorOptions`.

| Knob | Type | Default | Effect |
|---|---|---|---|
| `length` (first argument) | `int` | `25` | Total characters produced. Must be greater than zero, or `ArgumentException` is thrown. |
| `options` (second argument) | `StringCreatorOptions?` | `null` → a fresh `StringCreatorOptions` | Quota configuration. |
| `StringCreatorOptions.MinNumbers` | `int` | `0` | Exact number of digits included, drawn from `1234567890`. |
| `StringCreatorOptions.MinSpecials` | `int` | `0` | Exact number of symbols included, drawn from the 30-character pool ``!@#$%^&*()-_=+[]{}\|;:',.<>/?`~``. |

`MinNumbers + MinSpecials` must be strictly less than `length`; the remainder is filled from the 52-character
`a-z`/`A-Z` pool and the whole buffer is then shuffled with `RandomNumberGenerator.Shuffle`. There is no way to
supply your own character pools and no seed, so output is never reproducible.

## Documentation

Full feature guide, configuration reference, and gotchas:
https://github.com/baoduy/DKNet/blob/main/docs/Core/DKNet.RandomCreator.md

## License

MIT © drunkcoding.net — https://github.com/baoduy/DKNet
