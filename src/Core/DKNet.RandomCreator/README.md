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

## Documentation

Full feature guide, configuration reference, and gotchas:
https://github.com/baoduy/DKNet/blob/dev/docs/Core/DKNet.RandomCreator.md

## License

MIT © drunkcoding.net — https://github.com/baoduy/DKNet
