---
name: dknet-core-utilities
description: "Covers DKNet.Fw.Extensions and DKNet.RandomCreator, the two dependency-light Core packages DKNet builds on. Fw.Extensions gives GetPropertyValue/SetPropertyValue/TrySetPropertyValue by dotted property path, IsImplementOf for open-generic type checks, TryConvertToEnum, enum [Display] attribute name lookup via GetEnumInfos<T>(), IsRegistered<T>()/IsRegisteredWithImplementation<T>() DI guards on IServiceCollection, and the lazy TypeExtractors assembly scanner to scan assemblies for types, e.g. Extract().Abstract().Publics() or Extract().Classes().NotAbstract().IsInstanceOf<T>(). RandomCreator gives RandomCreators.NewString/NewChars, a CSPRNG-only generator with StringCreatorOptions MinNumbers/MinSpecials exact quotas, to generate a secure password, token, or one-time code. Use when reading or writing object fields by name, checking an open-generic implementation, guarding a duplicate DI registration, converting a loose value to an enum, or generating a secure random string."
license: MIT
metadata:
  author: baoduy
  version: "0.1.0"
  packages: "DKNet.Fw.Extensions,DKNet.RandomCreator"
---

# DKNet Core Utilities

This skill covers the two Core packages the rest of the DKNet suite sits on: **DKNet.Fw.Extensions** (reflection, type-shape, enum, and DI-registration helpers, plus the `TypeExtractors` assembly scanner) and **DKNet.RandomCreator** (a CSPRNG-only random string/char generator). Both are dependency-light, static-method libraries with zero DI registration of their own — reference the package, add the right `using`, call the method. Read `references/DKNet.Fw.Extensions.md` and `references/DKNet.RandomCreator.md` for the full public surface, every gotcha, and every hallucination trap.

## Packages

| Package | Install | What it gives you | Depends on | Reference |
|---|---|---|---|---|
| `DKNet.Fw.Extensions` | `dotnet add package DKNet.Fw.Extensions` | Reflection property access by name/dotted path, open-generic type checks, enum `[Display]` metadata, DI registration guards, the lazy `TypeExtractors` assembly/type scanner | none (DKNet); `Microsoft.Extensions.DependencyInjection.Abstractions`, `System.ComponentModel.Annotations` | [references/DKNet.Fw.Extensions.md](references/DKNet.Fw.Extensions.md) |
| `DKNet.RandomCreator` | `dotnet add package DKNet.RandomCreator` | CSPRNG-backed random `string`/`char[]` generation with exact digit/symbol quotas | none | [references/DKNet.RandomCreator.md](references/DKNet.RandomCreator.md) |

## Quick start

Neither package needs DI wiring to be usable — the only thing worth registering is code you write on top of them. This guards a DI registration with `IsRegistered<T>()` (`DKNet.Fw.Extensions`, ambient in `Microsoft.Extensions.DependencyInjection` — no extra `using` needed) and issues the token with `RandomCreators.NewString` (`DKNet.RandomCreator`):

```csharp
using DKNet.RandomCreator;

public interface IApiTokenIssuer
{
    string IssueToken();
}

public sealed class ApiTokenIssuer : IApiTokenIssuer
{
    public string IssueToken() =>
        RandomCreators.NewString(40, new StringCreatorOptions { MinNumbers = 8, MinSpecials = 4 });
}

public static class ApiTokenServiceCollectionExtensions
{
    public static IServiceCollection AddApiTokenIssuer(this IServiceCollection services)
    {
        if (!services.IsRegistered<IApiTokenIssuer>())
            services.AddSingleton<IApiTokenIssuer, ApiTokenIssuer>();

        return services;
    }
}

public static class QuickStartDemo
{
    public static void Run()
    {
        var services = new ServiceCollection();
        services.AddApiTokenIssuer();
        services.AddApiTokenIssuer(); // safe to call twice — IsRegistered<T> guard skips the second AddSingleton

        using (var provider = services.BuildServiceProvider())
        {
            var issuer = provider.GetRequiredService<IApiTokenIssuer>();
            var token = issuer.IssueToken();
        }
    }
}
```

## Rules

1. Import the right sub-namespace. There is no flat `DKNet.Fw.Extensions` namespace — `Reflection`, `Primitives`, `Enums`, `Collections`, `TypeExtractors` are separate. `using DKNet.Fw.Extensions;` alone resolves nothing.
2. Never treat `IsImplementOf` as an identity check. `type.IsImplementOf(type)` (same type both sides) always returns `false` — it means "implements/inherits", not "is".
3. Always assign the result of a `TypeExtractor` filter call. `Abstract()`, `NotAbstract()`, `Classes()`, `Where(...)`, and every other filter return a **new** `ITypeExtractor` and never mutate the one you called them on.
4. Reach `ITypeExtractor` only through `Extract()`. `TypeExtractor` itself is `internal` — `Extract()` on an `Assembly`/`Assembly[]`/`ICollection<Assembly>` is the only entry point.
5. Use `TrySetPropertyValue` only when a missing/mismatched property must not abort the caller. It still throws `ArgumentNullException`/`ArgumentException` for a null/empty `propertyName` — only the underlying property-not-found/format failure is swallowed, and anything else (e.g. `InvalidCastException`) still propagates.
6. Pick the DI guard that matches the contract's cardinality. `IsRegistered<TService>()` is first-wins — true for *any* implementation, so it blocks a second, different implementation too. Use `IsRegisteredWithImplementation<TService>(Type)` when a second, distinct implementation of the same contract must be allowed to coexist.
7. Null-check `EnumInfo.Name` after the **instance** `enumValue.GetEnumInfo()` call, not after the static `EnumExtensions.GetEnumInfos<T>()`. The instance method has no field-name fallback and can hand back `Name = null` at runtime despite the `required string` annotation.
8. `TryConvertToEnum` converts an underlying numeric value, not a member-name string. It runs the value through `Convert.ChangeType` to the enum's underlying integral type, then `Enum.ToObject`s it — `1` or `"1"` convert to the enum member with value `1`, but the string `"Express"` does not convert to `ShippingMethod.Express`. Use `Enum.TryParse` (BCL) for member-name strings.
9. Keep `StringCreatorOptions.MinNumbers + MinSpecials` strictly less than `length`. The guard is `>=`, so a sum *equal* to `length` already throws `ArgumentException` — leave at least one filler slot.
10. Treat `MinNumbers`/`MinSpecials` as exact counts, not minimums. The filler beyond the quota is drawn purely from the 52-letter pool; asking for `MinNumbers = 5` never yields extra digits.
11. Choose `RandomCreators.NewChars` over `NewString` for anything short-lived and sensitive (OTP, one-time secret). Only the `char[]` form can be wiped (`Array.Clear`) after use; a `string` is immutable and can linger in managed memory.
12. Never use `DKNet.RandomCreator` for seeded/repeatable output. There is no seed anywhere in the API — for deterministic test fixtures use a fixed literal instead.

## How to ...

### Read or write an object's properties by name, including nested paths

**When**: mapping loosely-typed input (a form dictionary, a DTO) onto a domain object without hand-writing per-field assignment.

```csharp
using DKNet.Fw.Extensions.Reflection;

public class Address
{
    public string City { get; set; } = string.Empty;
}

public class Owner
{
    public Address Address { get; set; } = new();
}

public class Product
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public Owner Owner { get; set; } = new();
}

public static class PropertyPathDemo
{
    public static void Run()
    {
        var product = new Product
        {
            Name = "Laptop",
            Price = 999.99m,
            Owner = new Owner { Address = new Address { City = "Hanoi" } }
        };

        var name = product.GetPropertyValue("name");                // "Laptop" — case-insensitive
        product.SetPropertyValue("Price", "1099.99");                // string -> decimal via Convert.ChangeType
        var city = product.GetPropertyValue("Owner.Address.City");   // "Hanoi" — dotted path walks nested objects

        product.TrySetPropertyValue("DoesNotExist", 1);              // swallowed — logs via Debug.WriteLine, no throw
    }
}
```

**Notes**:
- `GetPropertyValue` returns `null` as soon as any path segment is missing or an intermediate value is `null` — it never throws for a bad path.
- `SetPropertyValue(string, object)` throws `ArgumentException` if the named property does not exist.
- Every `(Type, propertyName, flags)` lookup is cached in a static dictionary — repeated calls do not re-run reflection.

### Check whether a type implements an open generic interface or base class

**When**: a convention (handler discovery, an EF Core model builder) must match `IRepository<Product>` against `IRepository<>` without enumerating every closed generic by hand.

```csharp
using DKNet.Fw.Extensions.Reflection;

public interface IRepository<T>;

public class Invoice;

public class InvoiceRepository : IRepository<Invoice>;

public static class ImplementOfDemo
{
    public static void Run()
    {
        var matches = typeof(InvoiceRepository).IsImplementOf(typeof(IRepository<>));      // true
        var sameType = typeof(InvoiceRepository).IsImplementOf(typeof(InvoiceRepository));  // false — implements, not "is"
    }
}
```

**Notes**:
- `IsImplementOf` walks open/closed interfaces and base classes up the hierarchy; see Rule 2 for the identity trap.

### Auto-discover types across assemblies by shape

**When**: registering every non-abstract class that implements a marker interface, instead of hand-listing types.

```csharp
using System.Reflection;
using DKNet.Fw.Extensions.TypeExtractors;

public interface IEventHandler;

public class OrderCreatedHandler : IEventHandler;

public static class HandlerScanDemo
{
    public static List<Type> Run()
    {
        Assembly[] assemblies = [typeof(OrderCreatedHandler).Assembly];

        return assemblies
            .Extract()
            .Classes()
            .NotAbstract()
            .IsInstanceOf<IEventHandler>()
            .ToList();
    }
}
```

**Notes**:
- Nothing is scanned until the chain is enumerated (`ToList()`, `foreach`, LINQ — `ITypeExtractor : IEnumerable<Type>`).
- `asm.GetTypes()` is cached per `Assembly` for the process lifetime; only the predicate chain re-runs on every enumeration.

### Guard a DI registration against being added twice

**When**: a library's `AddXyz(IServiceCollection)` extension may be called by more than one feature module and must stay idempotent.

```csharp
public interface IIdempotencyKeyStore;

public class SqlIdempotencyKeyStore : IIdempotencyKeyStore;

public static class IdempotencyStoreServiceCollectionExtensions
{
    public static IServiceCollection AddIdempotencyStore(this IServiceCollection services)
    {
        if (!services.IsRegistered<IIdempotencyKeyStore>())
            services.AddSingleton<IIdempotencyKeyStore, SqlIdempotencyKeyStore>();

        return services;
    }
}

public static class IdempotencyStoreDemo
{
    public static void Run()
    {
        var services = new ServiceCollection();
        services.AddIdempotencyStore();
        services.AddIdempotencyStore(); // second call is a no-op — IsRegistered<T>() already sees IIdempotencyKeyStore
    }
}
```

**Notes**:
- `IsRegistered<T>()` is a first-wins guard: it reports "already registered" for any implementation of `T` — see Rule 6.
- No `using` beyond the package under test is needed here — the guard lives in the ambient `Microsoft.Extensions.DependencyInjection` namespace.

### Map an enum's [Display] metadata for a UI dropdown

**When**: rendering `[Display(Name=...)]` metadata without hand-rolling a switch statement.

```csharp
using System.ComponentModel.DataAnnotations;
using DKNet.Fw.Extensions.Enums;

public enum OrderStatus
{
    [Display(Name = "Pending", Description = "Waiting for processing")]
    Pending,
    Processing,
}

public static class EnumDisplayDemo
{
    public static void Run()
    {
        foreach (var info in EnumExtensions.GetEnumInfos<OrderStatus>())
        {
            var line = $"{info.Key}: {info.Name}";
            Console.WriteLine(line);
            // Pending: Pending
            // Processing: Processing  (Name falls back to the field name — no [Display] on this value)
        }
    }
}
```

**Notes**:
- The static `GetEnumInfos<T>()` falls back to the field name when a value has no `[Display]`; the single-value instance `GetEnumInfo()` does not — see Rule 7.

### Safely convert a loosely-typed value to an enum

**When**: a value from a database column or JSON payload (an `int`, or a numeric string) needs to become a strongly-typed enum without a hand-rolled try/catch around `Enum.ToObject`.

```csharp
using DKNet.Fw.Extensions.Reflection;

public enum ShippingMethod
{
    Standard = 0,
    Express = 1,
}

public static class EnumConversionDemo
{
    public static void Run()
    {
        object raw = 1; // e.g. read from a database column or JSON number

        if (raw.TryConvertToEnum<ShippingMethod>(out var method))
        {
            var chosen = method; // ShippingMethod.Express
        }

        object badRaw = "not-a-number";
        var ok = badRaw.TryConvertToEnum<ShippingMethod>(out var fallback); // false — can't convert to the underlying int
    }
}
```

**Notes**:
- Only `InvalidCastException`, `FormatException`, and `OverflowException` from the conversion are caught; anything else propagates.
- This converts the underlying numeric value, not a member name — see Rule 8.

### Generate a secure random password or token with exact character quotas

**When**: a rule like "must contain exactly 4 digits and 2 symbols" needs no post-generation check.

```csharp
using DKNet.RandomCreator;

public static class PasswordDemo
{
    public static string Run()
    {
        var options = new StringCreatorOptions
        {
            MinNumbers = 4,
            MinSpecials = 2
        };

        // 32 characters total: exactly 4 digits, exactly 2 symbols, the remaining 26 are letters.
        return RandomCreators.NewString(32, options);
    }
}
```

**Notes**:
- `MinNumbers + MinSpecials` (here 6) must be strictly less than `length` (32) — see Rule 9.
- The whole buffer is shuffled with a CSPRNG before being returned, so the quota characters are not clumped at the front.

### Generate a wipeable one-time code

**When**: the value is sensitive and the buffer should be overwritten after use instead of relying on an immutable `string`.

```csharp
using DKNet.RandomCreator;

public static class OneTimeCodeDemo
{
    public static void Run()
    {
        var otp = RandomCreators.NewChars(7, new StringCreatorOptions { MinNumbers = 6 });
        try
        {
            SendOneTimeCode(otp);
        }
        finally
        {
            Array.Clear(otp);
        }
    }

    private static void SendOneTimeCode(char[] code)
    {
        // deliver code
    }
}
```

**Notes**:
- `NewChars` returns the raw buffer so the caller can `Array.Clear` it; `NewString` wraps the same buffer in an immutable `string` that cannot be scrubbed — see Rule 11.

## Runtime behaviour

- **Property access** (`GetPropertyValue`/`SetPropertyValue`/`TrySetPropertyValue`): a static `ConcurrentDictionary<(Type, string, BindingFlags), PropertyInfo?>` is checked first; on a miss, `Type.GetProperty` runs once and the result (including a cached `null` for "not found") is stored. `GetPropertyValue` then walks each dot-separated segment, stopping at the first `null`.
- **Type scanning** (`TypeExtractors`): each filter call (`Classes()`, `IsInstanceOf<T>()`, ...) only appends a compiled predicate to an immutable list and returns a new extractor — no reflection runs yet. Enumerating the result resolves each assembly's types through a static per-assembly cache (`GetTypes()` runs once per assembly, ever), then applies every predicate in the order the filters were chained.
- **Random generation** (`RandomCreators`): `NewString`/`NewChars` validate `length > 0` and `MinNumbers + MinSpecials < length`, allocate one buffer, fill exactly `MinNumbers` digit slots and `MinSpecials` symbol slots from `RandomNumberGenerator`, fill the remainder from the 52-letter pool, then `RandomNumberGenerator.Shuffle` the whole buffer in place before returning it.

## Gotchas

- **`SetPropertyValue`/`TrySetPropertyValue` swallow different exceptions per overload.** The `string`-keyed overload swallows `ArgumentNullException`/`ArgumentException`; the `PropertyInfo`-keyed overload swallows `ArgumentNullException`/`FormatException`. A conversion failure that raises `InvalidCastException` still propagates from either — don't treat `TrySet*` as a blanket try/catch.
- **`GetEnumInfo()` (the instance method) can hand back a `null` `Name` despite `EnumInfo.Name` being `required string`.** It assigns `Name = att?.Name!` with no fallback. Indexing into `.Name` (e.g. `.Length`) on a value without `[Display(Name=...)]` throws `NullReferenceException` at runtime, not at compile time.
- **`IsNumber()` is a loose heuristic, not `decimal.TryParse`.** It only enforces "at most one `.`", "no repeated `,,`", "a `-` only at index 0" — it accepts both `"123,456.789"` and `"123.456,789"` as "numeric" and rejects `"123-456"`. Don't use it to validate real numeric input.
- **`DateTime.LastDayOfMonth()` preserves `Kind` — it does not force `Local`.** The shipped implementation is `date.AddDays(...)`, which keeps the input's `Kind` untouched (a `Utc` input stays `Utc`). Don't add a manual `DateTime.SpecifyKind` "fix" after calling it.
- **Reflection-by-name here is not trim/AOT-safe.** `GetProperty` carries an `IL2075` trim-warning suppression and the package itself ships `IsTrimmable=false`. Don't enable `PublishTrimmed=true` and expect `GetPropertyValue`/`SetPropertyValue`/`TypeExtractors` to keep resolving members by name.
- **A `TypeExtractor`'s per-assembly type cache never refreshes.** `GetTypes()` is cached once per `Assembly` for the process lifetime. Loading or modifying a collectible assembly at runtime will not be reflected in a scan that already touched that assembly.
- **`IsKeyedImplementationOf` compares the keyed-service key by reference, not equality.** The check is `ReferenceEquals(descriptor.ServiceKey, keyName)` — pass the exact key object used at registration, not merely an equal one.

## Do not

- `date.Quarter()` does not exist — the real name is `InQuarter()`.
- `services.Register<T>()` / `services.HasRegistered<T>()` do not exist — the real names are `IsRegistered<TService>()` and `IsRegisteredWithImplementation<TService>(Type)`.
- `typeof(X).Implements(typeof(Y))` does not exist — the real name is `IsImplementOf`, defined on `Type`/`Type?`.
- `assembly.GetTypes().Extract()` is backwards — `Extract()` is the entry point *into* the scanner, called on `Assembly`/`Assembly[]`/`ICollection<Assembly>`, not after you already have a `Type[]`.
- `new TypeExtractor(...)` cannot be called from consumer code — `TypeExtractor` is `internal`; go through `Extract()`.
- `IAsyncEnumerable<T>.ToListAsync()` is not a DKNet extension — it was removed. Use the BCL `System.Linq.AsyncEnumerable.ToListAsync` (`using System.Linq;`) instead.
- `StringCreatorOptions.AlphabeticOnly` does not exist — it is a commented-out property that never compiled in. Letters-only output is the implicit result of leaving `MinNumbers`/`MinSpecials` at `0`.
- `new StringCreator(...)` cannot be called from consumer code — `StringCreator` is `internal`; go through `RandomCreators.NewChars`/`NewString`.
- `RandomCreators.NewToken(...)`, `.NewPassword(...)`, `.NewGuidString(...)` do not exist — the only two public methods are `NewChars` and `NewString`.
- `services.AddRandomCreator()` / `IRandomCreator` do not exist — the package has no DI surface at all; it is plain static methods.
- `StringCreatorOptions.Seed` / `.WithSeed(...)` do not exist — output is never deterministic.
- `StringCreatorOptions.CharPool` / `.Alphabet` do not exist — the three pools (52 letters / 10 digits / 30 symbols) are fixed `const` strings.

## Related skills

- `dknet-packages` — start there to choose which DKNet package solves a scenario before drilling into this one.
- `dknet-efcore-domain-model` — entity base classes and EF Core model wiring once a domain object needs persistence, not just in-memory reflection.
- `dknet-efcore-specifications` — querying persisted entities and dynamic predicates; this skill only covers in-memory reflection/randomness.
- `dknet-efcore-save-pipeline` — SaveChanges hooks, domain events and audit logs once entities are wired into a `DbContext`.
- `dknet-efcore-data-security` — row-level data authorization and column encryption for persisted data.
- `dknet-codegen` — `[GenerateDto]`/`[CrudAction]` source generators; do not hand-write what they emit.
- `dknet-slimbus-cqrs` — CQRS handlers and messaging once a request needs to reach a handler, not just a DI guard.
- `dknet-aspcore-api` — minimal-API endpoint conventions and startup tasks for the service you register with `IsRegistered<T>()`.
- `dknet-idempotency` — idempotent endpoints and key stores; a natural consumer of `RandomCreators` for generating store keys/tokens.
- `dknet-blob-storage` — blob storage abstraction/adapters; unrelated to this skill's reflection/random helpers.
- `dknet-services` — application services (encryption, PDF, templates); reach for `DKNet.Svc.Encryption` there instead of `DKNet.RandomCreator` for real cryptography.
- `dknet-testing` — writing or debugging tests for code that uses these helpers, including the DKNet repo's own test conventions.

## References

- [references/DKNet.Fw.Extensions.md](references/DKNet.Fw.Extensions.md) — full public surface: reflection property access, type-shape checks, enum `[Display]` metadata, DI guards, the `TypeExtractors` scanner, options/defaults, diagnostics, gotchas, and anti-patterns. Docs: https://github.com/baoduy/DKNet/blob/dev/docs/Core/DKNet.Fw.Extensions.md · NuGet: https://www.nuget.org/packages/DKNet.Fw.Extensions
- [references/DKNet.RandomCreator.md](references/DKNet.RandomCreator.md) — full public surface: `RandomCreators.NewString`/`NewChars`, `StringCreatorOptions`, the CSPRNG generation order, diagnostics, gotchas, and anti-patterns. Docs: https://github.com/baoduy/DKNet/blob/dev/docs/Core/DKNet.RandomCreator.md · NuGet: https://www.nuget.org/packages/DKNet.RandomCreator
- Full docs site: https://baoduy.github.io/DKNet/
