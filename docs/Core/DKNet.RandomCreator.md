# DKNet.RandomCreator

**A tiny, cryptographically secure random string and character generator for passwords, tokens, and other secrets.**

## What problem does it solve?

Anywhere your code needs an unpredictable value — a one-time password, an API token, a temporary secret, an invite code — `System.Random` is the wrong tool: it is not cryptographically secure and is trivially predictable. `DKNet.RandomCreator` wraps `System.Security.Cryptography.RandomNumberGenerator` behind a two-method static API (`RandomCreators.NewString` / `RandomCreators.NewChars`) so callers get secure randomness without touching the crypto APIs directly, plus simple quotas for guaranteeing a minimum number of digits and symbols (useful for password-strength rules).

Reach for it when you need a random string/char array and want it to actually be safe to use as a secret. It is not a general-purpose test-data/fixture generator (no seeding, no repeatable sequences).

## Install and minimum usage

```bash
dotnet add package DKNet.RandomCreator
```

```csharp
using DKNet.RandomCreator;

// 25-character random string (default length), letters only
var token = RandomCreators.NewString();
```

## Features

### 1. `RandomCreators.NewString(int length = 25, StringCreatorOptions? options = null)`

Returns a random `string` of the requested `length`. Saves you from hand-rolling a `RandomNumberGenerator` + `StringBuilder` loop every time you need a secret value.

```csharp
using DKNet.RandomCreator;

string sessionToken = RandomCreators.NewString(32);
```

### 2. `RandomCreators.NewChars(int length = 25, StringCreatorOptions? options = null)`

Same generation logic as `NewString`, but returns a `char[]` instead of a `string`. Useful when you want to overwrite/clear the buffer yourself after use (e.g. sensitive one-time codes) instead of relying on an immutable `string` that lingers in memory.

```csharp
using DKNet.RandomCreator;

char[] otpChars = RandomCreators.NewChars(6);
```

### 3. `StringCreatorOptions.MinNumbers` (default `0`)

Guarantees the output contains this many digit characters (`0-9`), drawn from a 10-character digit pool. These are exact quotas, not just a floor — see [Gotchas](#gotchas-and-limits).

### 4. `StringCreatorOptions.MinSpecials` (default `0`)

Guarantees the output contains this many special/symbol characters, drawn from a fixed 30-character symbol pool (`!@#$%^&*()-_=+[]{}|;:',.<>/?`~`). Combine with `MinNumbers` to build password-strength rules:

```csharp
using DKNet.RandomCreator;

var options = new StringCreatorOptions
{
    MinNumbers = 4,
    MinSpecials = 2
};

// 32 characters total: at least 4 digits, at least 2 symbols, rest letters
string strongPassword = RandomCreators.NewString(32, options);
```

### 5. Alphabetic-only output (default options)

There is no dedicated "alphabetic only" flag — it's implicit. When both `MinNumbers` and `MinSpecials` are left at their default of `0` (i.e. `new StringCreatorOptions()`, or simply omitting `options`), every character comes from the 52-character letter pool (`a-z`, `A-Z`) because that pool is the only one used to fill the remaining length. This is the correct way to get a letters-only string; there is nothing else to configure.

```csharp
using DKNet.RandomCreator;

// Letters only (a-z, A-Z) — default options already behave this way.
string alphaOnly = RandomCreators.NewString(16);
```

### 6. Uniform, shuffled output

Whatever mix of digits/symbols/letters is generated, the final character order is shuffled with `RandomNumberGenerator.Shuffle` before being returned — the required digits/specials are not clumped at the start of the string, and the symbol/letter pools are checked for uniform draw frequency in the package's own test suite (`RandomCreatorTests/UniformityTests.cs`).

### 7. Cryptographically secure randomness

All character selection (`RandomNumberGenerator.GetItems<char>`) and the final shuffle (`RandomNumberGenerator.Shuffle`) go through `System.Security.Cryptography.RandomNumberGenerator` — a CSPRNG, not `System.Random`. This is a verified fact from the source (`StringCreator.cs`), not a marketing claim.

## Configuration reference: `StringCreatorOptions`

| Property | Type | Default | Meaning |
|---|---|---|---|
| `MinNumbers` | `int` | `0` | Exact number of digit characters (`0-9`) included in the output. |
| `MinSpecials` | `int` | `0` | Exact number of symbol characters included in the output, from the pool `!@#$%^&*()-_=+[]{}|;:',.<>/?`~` (30 distinct characters). |

There is no property to customize the character pools, no case-only toggle, and no seed/repeatability option — the package intentionally does one narrow thing.

## Composition with other DKNet packages

`DKNet.RandomCreator` is a standalone utility: its `.csproj` declares no `PackageReference` and no `ProjectReference` to any other DKNet package (or any third-party library) — it depends only on the .NET base class library (`System.Security.Cryptography`). Use it anywhere in a solution, including from other DKNet packages, without pulling in additional dependencies. For application-grade cryptography (AES/RSA encryption, hashing, HMAC) rather than random value generation, use a dedicated encryption package (e.g. `DKNet.Svc.Encryption`) instead — this package does not attempt that.

## Gotchas and limits

- **`length` must be positive.** `NewString(0)` / `NewChars(0)` (and negative lengths) throw `ArgumentException`.
- **`MinNumbers + MinSpecials` must be strictly less than `length`.** If the sum is `>=` the requested length, both `NewString` and `NewChars` throw `ArgumentException` — there is no silent clamping. Always leave room for at least one filler (letter) character.
- **`MinNumbers`/`MinSpecials` are exact quotas, not loose minimums.** The implementation generates exactly that many digits and exactly that many symbols, then fills the rest of the length using the letter pool only — the filler never adds extra digits or symbols beyond your quotas. In other words, a request for `MinNumbers = 5` produces exactly 5 digits in the output, never more.
- **The character pools are fixed** — 52 letters, 10 digits, 30 symbols — and cannot be swapped for a custom alphabet.
- **Randomness is a genuine CSPRNG** (`RandomNumberGenerator`), so output is safe to use for secrets; it is not seedable, so results cannot be reproduced for tests — use a fixed literal instead of this generator when a test needs a deterministic value.
