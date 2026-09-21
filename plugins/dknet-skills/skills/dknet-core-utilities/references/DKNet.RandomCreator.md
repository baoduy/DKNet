# DKNet.RandomCreator

| Field | Value |
|---|---|
| Area | Core |
| NuGet | `dotnet add package DKNet.RandomCreator` |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/Core/DKNet.RandomCreator.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/Core/DKNet.RandomCreator |
| Depends on (DKNet) | none |
| Depends on (3rd party) | none — only the .NET BCL (`System.Security.Cryptography`) |
| Target framework | net10.0 |

## Purpose

`DKNet.RandomCreator` generates cryptographically secure random `string`/`char[]` values through two static entry points, `RandomCreators.NewString` and `RandomCreators.NewChars`. Internally it draws every character — including any digit/symbol quota and the final shuffle — from `System.Security.Cryptography.RandomNumberGenerator` (a CSPRNG), never `System.Random`, so the output is safe to hand out as a password, token, OTP, or invite code. It is **not** a seeded/repeatable test-data generator (there is no seed option) and it is **not** an encryption/hashing library — use `DKNet.Svc.Encryption` for AES/RSA/HMAC.

## Entry points

| Call | Exact signature | Called on | Notes (lifetime, ordering, prerequisites) |
|---|---|---|---|
| `RandomCreators.NewString` | `public static string NewString(int length = 25, StringCreatorOptions? options = null)` | Plain static method call — no DI, no attribute, no builder | Stateless; no registration needed. Throws `ArgumentException` if `length <= 0` or `options.MinNumbers + options.MinSpecials >= length`. |
| `RandomCreators.NewChars` | `public static char[] NewChars(int length = 25, StringCreatorOptions? options = null)` | Plain static method call | Same validation and behavior as `NewString`, but returns the mutable buffer instead of wrapping it in a `string` — caller owns it and may wipe it (e.g. `Array.Clear`) after use. |

There is no `IServiceCollection` extension, no `ModelBuilder`/`DbContextOptionsBuilder` hook, no attribute, and no MSBuild/analyzer surface in this package — it is a pure static utility library.

## Public surface

### `DKNet.RandomCreator` (namespace matches the package/assembly name — this is the project root, no folder-per-concern subdivision needed for a 3-file package)

| Type | Kind | Purpose | Key members with exact signatures |
|---|---|---|---|
| `RandomCreators` | `public static class` | The only public API surface consumers call. | `public static char[] NewChars(int length = 25, StringCreatorOptions? options = null)`; `public static string NewString(int length = 25, StringCreatorOptions? options = null)` |
| `StringCreatorOptions` | `public sealed class` | Quota configuration passed as the optional second argument. | `public int MinNumbers { get; set; }` (default `0`); `public int MinSpecials { get; set; }` (default `0`). No constructor parameters — plain object-initializer usage. |
| `StringCreator` | `internal sealed class` | Does the actual generation; not directly reachable from consumer code, but its logic is what determines observable output/exceptions. | `internal StringCreator(int bufferLength, StringCreatorOptions options)`; `public char[] ToChars()`; `public override string ToString()`. Declares three fixed character pools: `DefaultChars` (52 letters `a-z`+`A-Z`), `DefaultNumbers` (`"1234567890"`, 10 digits), `DefaultSymbols` (`` "!@#$%^&*()-_=+[]{}|;:',.<>/?`~" ``, 30 distinct symbols). |

There is no interface, no record, no enum, and no attribute in this package.

## Options & defaults

| Option | Type | Default | Effect | Where set |
|---|---|---|---|---|
| `length` (method parameter, not on the options object) | `int` | `25` | Total number of characters produced. Must be `> 0` or `NewString`/`NewChars` throw `ArgumentException`. | First positional argument to `NewString`/`NewChars` |
| `options` (method parameter) | `StringCreatorOptions?` | `null` → a fresh `new StringCreatorOptions()` is used internally | Supplies the quota configuration below | Second positional argument to `NewString`/`NewChars` |
| `StringCreatorOptions.MinNumbers` | `int` | `0` | Exact count of digit characters drawn from the fixed 10-character pool `1234567890` and included in the output. Not a floor — exactly this many, never more. | `new StringCreatorOptions { MinNumbers = n }` |
| `StringCreatorOptions.MinSpecials` | `int` | `0` | Exact count of symbol characters drawn from the fixed 30-character pool `` !@#$%^&*()-_=+[]{}|;:',.<>/?`~ `` and included in the output. Not a floor. | `new StringCreatorOptions { MinSpecials = n }` |

There is no property to supply a custom character pool and no seed/determinism option. When both `MinNumbers` and `MinSpecials` are left at `0` (the default), every character comes from the 52-character letter pool — that is the (implicit) letters-only mode; there is no separate `AlphabeticOnly` flag.

## Usage patterns

### Generate a default 25-character letters-only token

**When**: you need a quick random string and don't care about digit/symbol quotas.

```csharp
using DKNet.RandomCreator;

public static class DefaultTokenExample
{
    public static string Generate() => RandomCreators.NewString(); // 25 chars, a-z/A-Z only (defaults)
}
```

**Notes**: default `length` is 25; default `options` is an empty `StringCreatorOptions` (both quotas 0), so every character comes from the letter pool.

### Build a password-strength secret with exact digit/symbol quotas

**When**: a rule like "must contain exactly 4 digits and 2 symbols" needs no post-generation check.

```csharp
using DKNet.RandomCreator;

public static class StrongPasswordExample
{
    public static string Generate()
    {
        var options = new StringCreatorOptions
        {
            MinNumbers = 4,
            MinSpecials = 2
        };

        // 32 characters total: exactly 4 digits, exactly 2 symbols, remaining 26 are letters.
        return RandomCreators.NewString(32, options);
    }
}
```

**Notes**: `MinNumbers + MinSpecials` (here 6) must be strictly less than `length` (32) — it is, so this succeeds. The digits/symbols are not clumped: the whole buffer is shuffled with `RandomNumberGenerator.Shuffle` before being returned.

### Generate a wipeable one-time code as `char[]`

**When**: the value is sensitive and you want to overwrite the buffer yourself instead of relying on an immutable `string` that lingers in managed memory.

```csharp
using DKNet.RandomCreator;

public static class OneTimeCodeExample
{
    public static void SendAndWipe()
    {
        var otp = RandomCreators.NewChars(7, new StringCreatorOptions { MinNumbers = 6 });
        try
        {
            Deliver(otp);
        }
        finally
        {
            Array.Clear(otp); // zero the buffer once it's no longer needed
        }
    }

    private static void Deliver(char[] code)
    {
        // send code
    }
}
```

**Notes**: `MinNumbers` must leave at least one filler slot below `length` (a `length: 6, MinNumbers: 6` call would violate the quota rule, `6 + 0 >= 6`, and throw) — this example uses `length: 7` with `MinNumbers: 6` to stay just inside the boundary. See Gotchas below for the exact rule.

### Letters-only output (explicit, no quotas)

**When**: you want an alphabetic-only string and want the intent to read clearly in code, even though it's just the default behavior.

```csharp
using DKNet.RandomCreator;

public static class LettersOnlyExample
{
    public static string Generate()
    {
        var lettersOnly = new StringCreatorOptions { MinNumbers = 0, MinSpecials = 0 };
        return RandomCreators.NewString(16, lettersOnly); // a-z, A-Z only
    }
}
```

**Notes**: identical result to omitting `options` entirely — there is no dedicated `AlphabeticOnly` flag (a commented-out property exists in `StringCreatorOptions` but is not compiled in; do not reference it).

### Handling invalid `length`/quota input

**When**: validating that a caller-supplied length or quota configuration is safe before generating a value.

```csharp
using DKNet.RandomCreator;

public static class InvalidQuotaExample
{
    public static void Generate()
    {
        try
        {
            RandomCreators.NewString(10, new StringCreatorOptions { MinNumbers = 5, MinSpecials = 5 });
        }
        catch (ArgumentException ex)
        {
            // MinNumbers (5) + MinSpecials (5) == length (10) -> throws because the sum must be strictly less than length.
            Console.WriteLine(ex.Message);
        }
    }
}
```

**Notes**: `length <= 0` and `MinNumbers + MinSpecials >= length` are the only two failure modes, both raised as `ArgumentException` — there is no custom exception type.

## Runtime behaviour

Both `NewString` and `NewChars` construct an internal `StringCreator(length, options ?? new StringCreatorOptions())` and call into it. `StringCreator.ToChars()` runs, in order:

1. Validate `bufferLength > 0`; throw `ArgumentException(nameof(bufferLength))` if not.
2. Validate `options.MinNumbers + options.MinSpecials < bufferLength`; throw `ArgumentException(nameof(options))` if the sum is `>=` the length.
3. Allocate one `char[bufferLength]` buffer (the only allocation returned to the caller — each segment is written straight into a slice of it).
4. If `MinNumbers > 0`: fill that many slots from the digit pool via `RandomNumberGenerator.GetItems<char>`; advance the offset.
5. If `MinSpecials > 0`: fill that many slots from the symbol pool via `RandomNumberGenerator.GetItems<char>`; advance the offset.
6. Fill the remainder purely from the 52-letter pool via `RandomNumberGenerator.GetItems<char>`.
7. `RandomNumberGenerator.Shuffle` the entire buffer in place, so the quota characters end up at random positions, not clumped at the front.
8. `ToChars()` returns the buffer; `ToString()` (used by `NewString`) wraps it as `new string(chars)`.

## Diagnostics & exceptions

| ID or exception type | Severity | When | Fix |
|---|---|---|---|
| `ArgumentException` (`nameof(bufferLength)`) | Error (thrown) | `length` passed to `NewString`/`NewChars` is `<= 0` | Pass a positive `length`. |
| `ArgumentException` (`nameof(options)`) | Error (thrown) | `options.MinNumbers + options.MinSpecials >= length` | Increase `length`, or lower `MinNumbers`/`MinSpecials` so their sum stays strictly less than `length` (leave room for at least one filler letter). |

No analyzer/source-generator diagnostics exist in this package — there are no `DiagnosticDescriptor` definitions anywhere in it.

## Gotchas

- **Quotas are exact, not floors.** `StringCreator.ToChars()` draws exactly `MinNumbers` digits and exactly `MinSpecials` symbols, then fills the rest purely from the letter pool — filler never adds bonus digits/symbols. Asking for `MinNumbers = 5` never yields 6+ digits.
- **The quota check uses `>=`, not `>`.** `MinNumbers + MinSpecials` must be *strictly less than* `length`; equal is already a throw (e.g. `length: 10, MinNumbers: 5, MinSpecials: 5` throws because `10 >= 10`).
- **`length` of `0` (or negative) always throws** — there is no empty-string/empty-array result.
- **Output is never reproducible.** There is no seed anywhere in the API. A test that needs a deterministic value must use a fixed literal instead of this generator; verifying "uniform draw" behavior means asserting *statistical* frequency bounds across many runs, not exact values.
- **`StringCreator` is `internal`.** Consumers cannot `new StringCreator(...)` directly from outside the assembly; the only access path is `RandomCreators.NewChars`/`NewString`.
- **The character pools are fixed and cannot be swapped.** 52 letters / 10 digits / 30 symbols, hardcoded inside `StringCreator`. A commented-out `AlphabeticOnly` property sits in `StringCreatorOptions` ("Commented out for future use") — it does not compile in and must not be referenced.
- **`NewChars` exists specifically so the caller can wipe the buffer** (e.g. `Array.Clear`) after use for sensitive values; `NewString` returns an immutable `string` that cannot be scrubbed and may linger in managed memory until GC. Choose `NewChars` for anything short-lived and sensitive (OTPs, one-time secrets).

## Anti-patterns & hallucination traps

- `StringCreatorOptions.AlphabeticOnly` — **does not exist** (compiled). It is a commented-out property in `StringCreatorOptions`. Letters-only output is achieved implicitly by leaving `MinNumbers`/`MinSpecials` at `0`.
- `new StringCreator(length, options)` — **not callable** from consumer code; `StringCreator` is `internal`. Always go through `RandomCreators.NewChars`/`NewString`.
- `RandomCreators.NewToken(...)`, `RandomCreators.NewPassword(...)`, `RandomCreators.NewGuidString(...)` — none of these exist. The only two public methods are `NewChars` and `NewString`.
- `services.AddRandomCreator()` / `IRandomCreator` / any DI registration — **there is no DI surface**. The package has zero `Microsoft.Extensions.DependencyInjection` usage; it's plain static methods.
- `StringCreatorOptions.Seed`, `.WithSeed(...)`, or any repeatability knob — **does not exist**; output is never deterministic (CSPRNG-backed).
- `StringCreatorOptions.CharPool` / `.Alphabet` / any custom-pool parameter — **does not exist**; the three pools are hardcoded inside `StringCreator`.
- Treating `MinNumbers`/`MinSpecials` as a *minimum that filler can exceed* — wrong; they are exact quotas, and the letter-pool filler never contributes digits or symbols.
- Assuming `MinNumbers + MinSpecials == length` is allowed (e.g. an all-digit code where quota equals length) — it throws; the sum must be strictly less than `length`.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.Svc.Encryption` | Reach for it instead when the need is application-grade cryptography (AES/RSA encryption, hashing, HMAC) rather than plain random value generation — `DKNet.RandomCreator` deliberately does not do that. |
| `DKNet.Fw.Extensions` | The other Core-area package (reflection/type/DI helpers). Not related to randomness; reach for it for everything in Core that isn't random-value generation. |
| Any package needing a secret/token/OTP value | `DKNet.RandomCreator` is the standalone, zero-dependency utility to reach for instead of hand-rolling a `RandomNumberGenerator` + `StringBuilder` loop, or (worse) using `System.Random` for anything security-sensitive. |

## Testing notes

- Fully stateless/self-contained — no fixtures, no containers, no mocking. Tests just call `RandomCreators.NewChars`/`NewString` directly and assert on the returned buffer's length and character-class composition (`char.IsLetter`, pool-membership via `string.Contains`).
- Because output is never seedable, correctness for the "uniform draw" claim has to be tested **statistically**, not by exact value: run several thousand iterations and assert the max/min draw-frequency ratio across the pool stays within a tight bound, and that every distinct symbol/letter in the pool is actually observed at least once.
- Cover the exact-quota boundary explicitly: a quota that fills the whole buffer minus one filler slot, and a quota where the remaining fill segment is exactly one character — both are easy to get subtly wrong in the shared buffer/offset math if you ever reimplement similar logic.
- The canonical exception test is the zero/negative-length case: assert `NewString(0)` (and `NewChars(0)`) throw `ArgumentException`.
