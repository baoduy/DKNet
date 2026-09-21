# DKNet.Fw.Extensions

| Field | Value |
|---|---|
| Area | Core |
| NuGet | `dotnet add package DKNet.Fw.Extensions` |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/Core/DKNet.Fw.Extensions.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/Core/DKNet.Fw.Extensions |
| Depends on (DKNet) | none |
| Depends on (3rd party) | `Microsoft.Extensions.DependencyInjection.Abstractions`, `System.ComponentModel.Annotations` |
| Target framework | net10.0 |

## Purpose

Dependency-light, framework-agnostic extension methods that the rest of the DKNet suite is built on: reflection-based property get/set (including dotted nested paths), type-shape checks that understand open generics (`IsImplementOf`), enum-to-`[Display]` metadata, string/date helpers, a lazy fluent assembly/type scanner (`TypeExtractors`), and small DI-registration guards (`IsRegistered<T>` / `IsRegisteredWithImplementation<T>`). Every member is a static/extension method — there is no DI container registration, no `IOptions<T>`, no startup wiring; referencing the package and adding the right `using` is the entire setup.

It is NOT a DI container, a mapping/serialization library, or a validation framework — it exposes raw reflection primitives that other DKNet packages build conventions on top of; do not reach for it to replace `AutoMapper`/`System.Text.Json` or to implement business validation.

## Entry points

| Call | Exact signature | Called on | Notes |
|---|---|---|---|
| `IsRegistered<TService>()` | `public bool IsRegistered<TService>()` (extension on `IServiceCollection`) | `IServiceCollection` | First-wins guard — true if *any* implementation of `TService` is already registered. No lifetime/order effect; pure read of the current descriptor list at the point it is called during startup composition. |
| `IsRegisteredWithImplementation<TService>(Type)` | `public bool IsRegisteredWithImplementation<TService>(Type implementationType)` (extension on `IServiceCollection`) | `IServiceCollection` | Exact `(TService, implementationType)` match only — lets a second, distinct implementation of the same service coexist. |
| `IsImplementationOf(Type)` / `IsImplementationOf<TImplement>()` | `public bool IsImplementationOf(Type implementationType)` / `public bool IsImplementationOf<TImplement>()` (extension on `ServiceDescriptor`) | `ServiceDescriptor` | Matches on `ServiceType` or `ImplementationType`. Called after indexing/enumerating an existing `IServiceCollection`. |
| `IsKeyedImplementationOf(object, Type)` / `IsKeyedImplementationOf<TImplement>(object)` | `public bool IsKeyedImplementationOf(object keyName, Type implementationType)` / `public bool IsKeyedImplementationOf<TImplement>(object keyName)` (extension on `ServiceDescriptor`) | `ServiceDescriptor` | Requires `descriptor.IsKeyedService == true` and `ReferenceEquals(descriptor.ServiceKey, keyName)` — key comparison is by reference, not `Equals`. |
| `Extract()` | `public static ITypeExtractor Extract(this Assembly assembly)` / `Extract(this Assembly[] assemblies)` / `Extract(this ICollection<Assembly> assemblies)` | `Assembly`, `Assembly[]`, `ICollection<Assembly>` | Entry point into the fluent `ITypeExtractor` chain. Throws `ArgumentException` if the resolved assembly array is null/empty. Duplicate assemblies are de-duplicated. Nothing is scanned until the result is enumerated. |

No `ModelBuilder`/`DbContextOptionsBuilder` extensions, no attributes, and no Roslyn analyzers/`DiagnosticDescriptor`s exist in this package.

## Public surface

### `DKNet.Fw.Extensions.Primitives`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `StringExtensions` | static class | String cleanup/classification and string/value-type shape checks. | `public static bool IsStringOrValueType(this PropertyInfo? propertyInfo)`; `public static bool IsStringOrValueType(this Type? type)`; extension block on `string input`: `public string ExtractDigits()`, `public bool IsNumber()` |
| `DateTimeExtensions` | static class | Calendar-quarter and end-of-month helpers. | `public static int InQuarter(this DateTime date)`; `public static DateTime? LastDayOfMonth(this DateTime? date)`; `public static DateTime LastDayOfMonth(this DateTime date)` |

### `DKNet.Fw.Extensions.Reflection`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `TypeExtensions` | static class | Type-shape checks (open generics), numeric-type detection, enum conversion. | `public static Type GetNonNullableType(this Type type)`; `public static bool IsAssignableFrom<TType>(this Type type)`; `public static bool IsAssignableTo<TType>(this Type type)`; `public static bool IsEnumType(this Type? type)`; `public static bool IsImplementOf(this Type? type, Type? matching)`; `public static bool IsImplementOf<T>(this Type type)`; `public static bool IsNumericType(this Type @this)`; `public static bool IsNumericType(this object? @this)`; `public static bool TryConvertToEnum(this Type enumType, object value, out object? result)`; `public static bool TryConvertToEnum<TEnum>(this object value, out TEnum? result) where TEnum : struct, Enum` |
| `PropertyExtensions` | static class | Cached reflection property lookup/get/set, including dotted nested paths. | `public static bool IsNullableType(this Type type)`; extension block on `T? obj where T : class` (also accepts `obj` as a `Type`): `public PropertyInfo? GetProperty(string propertyName, BindingFlags flags = IgnoreCase|Public|NonPublic|Instance)`, `public object? GetPropertyValue(string propertyName)`; extension block on `object obj`: `public void SetPropertyValue(PropertyInfo property, object? value)`, `public void SetPropertyValue(string propertyName, object value)`, `public void TrySetPropertyValue(string propertyName, object value)`, `public void TrySetPropertyValue(PropertyInfo property, object? value)` |
| `AttributeExtensions` | static class | Null-safe attribute-presence checks. | `public static bool HasAttribute<TAttribute>(this PropertyInfo? @this, bool inherit = true) where TAttribute : Attribute`; `public static bool HasAttribute<TAttribute>(this Type? @this, bool inherit = true) where TAttribute : Attribute`; `public static bool HasAttributeOnProperty<TAttribute>(this object @this, string propertyName, bool inherit = true) where TAttribute : Attribute` |

### `DKNet.Fw.Extensions.Enums`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `EnumExtensions` | static class | `[Display]`-attribute metadata for enum values. | `public static IEnumerable<EnumInfo> GetEnumInfos<T>() where T : Enum` (static); extension block on `Enum? @this`: `public T? GetAttribute<T>() where T : Attribute`, `public EnumInfo? GetEnumInfo()` |
| `EnumInfo` | sealed record | Immutable DTO for one enum value's display metadata. | `public required string Key { get; init; }`; `public required string Name { get; init; }`; `public string? Description { get; init; }`; `public string? GroupName { get; init; }` |

### `DKNet.Fw.Extensions.Collections`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `CollectionExtensions` | static class | Bulk-add helper for any `ICollection<T>`. | `public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)` |

### `DKNet.Fw.Extensions.TypeExtractors`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `ITypeExtractor` | interface, extends `IEnumerable<Type>` | Chainable, lazily-evaluated type filter over one or more assemblies. | Shape filters: `Abstract()`, `Classes()`, `Enums()`, `Generic()`, `Interfaces()`, `Nested()`, `Publics()`, and their `Not*` counterparts (`NotAbstract()`, `NotClass()`, `NotEnum()`, `NotGeneric()`, `NotInterface()`, `NotNested()`, `NotPublic()`). Relationship filters: `HasAttribute<TAttribute>() where TAttribute : Attribute`, `HasAttribute(Type attributeType)`, `IsInstanceOf(Type? type)`, `IsInstanceOf<T>()`, `IsInstanceOfAny(params Type[] types)`, `NotInstanceOf(Type? type)`, `NotInstanceOf<T>()`. Escape hatch: `Where(Expression<Func<Type, bool>>? predicate)`. |
| `TypeArrayExtractorExtensions` | static class | Entry points that construct an `ITypeExtractor`. | `public static ITypeExtractor Extract(this Assembly assembly)`; `public static ITypeExtractor Extract(this Assembly[] assemblies)`; `public static ITypeExtractor Extract(this ICollection<Assembly> assemblies)` |
| `TypeExtractor` | `internal class : ITypeExtractor` | The only implementation of `ITypeExtractor`; not constructible from consumer code. | `internal TypeExtractor(Assembly[] assemblies)` — throws `ArgumentException` if null/empty, de-duplicates via `.Distinct()`; every filter method (e.g. `Abstract()`, `IsInstanceOf(Type?)`) routes through a private `FilterBy` method, which returns a **new** `TypeExtractor` carrying the accumulated predicate list plus one more, compiled once; `GetEnumerator()` re-queries the assemblies through a static per-assembly type cache and applies every accumulated predicate with LINQ `.Where(...)` at enumeration time. |

### `Microsoft.Extensions.DependencyInjection` (ambient namespace — these types live at the package's project root, not grouped into a folder, so their extension methods resolve without an extra `using`)

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `ServiceCollectionExtensions` | static class | Inspects a `ServiceDescriptor`, including keyed registrations. | extension block on `ServiceDescriptor descriptor`: `public bool IsImplementationOf(Type implementationType)`, `public bool IsImplementationOf<TImplement>()`, `public bool IsKeyedImplementationOf(object keyName, Type implementationType)`, `public bool IsKeyedImplementationOf<TImplement>(object keyName)` |
| `ServiceCollectionRegistrationExtensions` | static class | Duplicate-registration guards for `IServiceCollection`. | extension block on `IServiceCollection services`: `public bool IsRegistered<TService>()`, `public bool IsRegisteredWithImplementation<TService>(Type implementationType)` |

No internal types beyond `TypeExtractor` explain observable behaviour; `PropertyExtensions`' cache field is private but its effect (identical `PropertyInfo` instances / no cache-collision across property names) is covered under Gotchas below.

## Options & defaults

No options object, no `IOptions<T>`, no environment-specific behavior exists anywhere in this package — every member is a static or extension method with fixed behavior. The only two configurable knobs are optional parameters, plus the `ITypeExtractor` filter chain (which is a call sequence, not a settings object):

| Option | Type | Default | Effect | Where set |
|---|---|---|---|---|
| `GetProperty(propertyName, flags)` — `flags` | `BindingFlags` | `BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance` | Which properties the reflection lookup considers. `GetPropertyValue`, `SetPropertyValue`, `TrySetPropertyValue`, and `HasAttributeOnProperty` all resolve through this default internally and do **not** expose the parameter themselves — only `GetProperty` itself takes it. | `PropertyExtensions.GetProperty`'s default parameter value |
| `HasAttribute<TAttribute>(inherit)` / `HasAttributeOnProperty<TAttribute>(propertyName, inherit)` — `inherit` | `bool` | `true` | Whether an attribute inherited from a base type/property counts as present (passed straight through to `Attribute.IsDefined`). | Each `AttributeExtensions` method's default parameter value |
| `ITypeExtractor` filter chain | chained method calls, not a settings object | no filter applied until called | Each call (`Classes()`, `NotAbstract()`, `IsInstanceOf<T>()`, `Where(...)`, ...) returns a **new** extractor with one more predicate appended; nothing is scanned until the result is enumerated (`ToList()`, `foreach`, LINQ). | `TypeExtractor`'s private `FilterBy` method, which every filter routes through |

## Usage patterns

### Reading/writing a property by name, including nested paths

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

public static class PropertyPathExample
{
    public static void ReadAndWrite()
    {
        var product = new Product
        {
            Name = "Laptop",
            Price = 999.99m,
            Owner = new Owner { Address = new Address { City = "Hanoi" } }
        };

        var name = product.GetPropertyValue("name");                // "Laptop" — case-insensitive lookup
        product.SetPropertyValue("Price", "1099.99");                // converts the string to decimal via Convert.ChangeType
        var city = product.GetPropertyValue("Owner.Address.City");   // "Hanoi" — dotted path walks nested objects
    }
}
```

**Notes**: `GetPropertyValue` returns `null` as soon as any segment is missing or the intermediate value is `null` — it never throws for a bad path. `SetPropertyValue(string, object)` throws `ArgumentException` if the named property does not exist; use `TrySetPropertyValue` (below) when a missing/mismatched field must not abort the whole operation.

### Best-effort property assignment that must not throw

**When**: bulk-populating an object from an untrusted/partial source (e.g. a CSV row) where one bad column should not fail the whole row.

```csharp
using DKNet.Fw.Extensions.Reflection;

public static class TrySetExample
{
    public static void PopulateBestEffort()
    {
        var product = new Product();
        product.TrySetPropertyValue("Name", "Laptop");   // sets it
        product.TrySetPropertyValue("DoesNotExist", 1);  // swallowed — property not found — logs via Debug.WriteLine, no throw
    }
}
```

**Notes**: `TrySetPropertyValue(string, object)` still throws `ArgumentNullException` if `propertyName` is `null` and `ArgumentException` if `propertyName` is empty (those checks run *before* the try block); it only swallows the exception thrown by the underlying `SetPropertyValue` call once past that guard — `ArgumentNullException`/`ArgumentException` for the string overload, `ArgumentNullException`/`FormatException` for the `PropertyInfo` overload. A conversion failure that raises something else (e.g. `InvalidCastException`) still propagates.

### Checking whether a type implements an open generic interface

**When**: writing a convention (e.g. an EF Core model builder or a handler-discovery rule) that must match `IRepository<Product>` against `IRepository<>` without enumerating every closed generic by hand.

```csharp
using DKNet.Fw.Extensions.Reflection;

public interface IRepository<T>;

public class ProductRepository : IRepository<Product>;

public static class ImplementOfExample
{
    public static void Check()
    {
        var matches = typeof(ProductRepository).IsImplementOf(typeof(IRepository<>));      // true
        var sameType = typeof(ProductRepository).IsImplementOf(typeof(ProductRepository));  // false — "implements", not "is"
    }
}
```

**Notes**: `IsImplementOf` walks interfaces (open or closed), base classes, and open generic base definitions up the hierarchy. It always returns `false` when `type == matching` — it answers "does this implement/inherit something else", not identity.

### Scanning assemblies for a shape of type (e.g. handler discovery)

**When**: auto-registering every non-abstract class that implements a marker interface, instead of hand-listing types.

```csharp
using System.Reflection;
using DKNet.Fw.Extensions.TypeExtractors;

public interface IEventHandler;

public class OrderCreatedHandler : IEventHandler;

public static class ScanExample
{
    public static List<Type> DiscoverHandlers()
    {
        Assembly[] assemblies = [typeof(OrderCreatedHandler).Assembly];

        return assemblies
            .Extract()
            .Classes()
            .NotAbstract()
            .IsInstanceOf<IEventHandler>()
            .ToList(); // ITypeExtractor : IEnumerable<Type>, so LINQ works directly on it
    }
}
```

**Notes**: nothing is scanned until `ToList()`/`foreach`/LINQ enumerates the chain. Every filter call returns a **new** extractor — branching a shared extractor into two mutually exclusive filters (e.g. `.Abstract()` and `.NotAbstract()`) never mutates the branch point, so it is safe to build one base extractor and fan it out. `Extract()` throws `ArgumentException` if given a null/empty assembly array, and de-duplicates repeated assemblies automatically.

### Guarding a DI registration against being added twice

**When**: a library's `AddXyz(IServiceCollection)` extension method may be called more than once (e.g. by two different feature modules) and must stay idempotent.

```csharp
public interface IIdempotencyKeyStore;

public class SqlIdempotencyKeyStore : IIdempotencyKeyStore;

public static class IdempotencySetupExample
{
    public static IServiceCollection AddIdempotencyStore(this IServiceCollection services)
    {
        if (!services.IsRegistered<IIdempotencyKeyStore>())
            services.AddSingleton<IIdempotencyKeyStore, SqlIdempotencyKeyStore>();

        return services;
    }
}
```

**Notes**: `IsRegistered<T>()` is a first-wins guard — it reports "already registered" for *any* implementation of `T`, so a second call with a different implementation is still treated as a duplicate and skipped. Use `IsRegisteredWithImplementation<T>(Type)` instead when a second, distinct implementation of a multi-implementation contract should be allowed to coexist.

### Enum-to-display-metadata mapping (e.g. for a dropdown)

**When**: rendering an enum's `[Display(Name=...)]` metadata without hand-rolling a switch statement.

```csharp
using System.ComponentModel.DataAnnotations;
using DKNet.Fw.Extensions.Enums;

public enum OrderStatus
{
    [Display(Name = "Pending", Description = "Waiting for processing")]
    Pending,
    Processing,
}

public static class EnumDisplayExample
{
    public static void Print()
    {
        foreach (var info in EnumExtensions.GetEnumInfos<OrderStatus>())
            Console.WriteLine($"{info.Key}: {info.Name}");
        // Pending: Pending
        // Processing: Processing   (Name falls back to the field name when there is no [Display])
    }
}
```

**Notes**: `GetEnumInfos<T>()` (the static, all-values method) falls back to the field name for `Name` when there is no `[Display]`. The single-value instance method `OrderStatus.Pending.GetEnumInfo()` does **not** fall back — it assigns `Name = att?.Name!`, so `Name` comes back `null` at runtime (despite `EnumInfo.Name` being declared `required string`) for any enum value without a `[Display(Name=...)]`. Null-check `Name` after calling the instance `GetEnumInfo()`, not after the static `GetEnumInfos<T>()`.

### Safely converting a raw value to an enum

**When**: a value from a database column or a JSON payload (an `int`, or a numeric string) needs to become a strongly-typed enum, with a single boolean check instead of a hand-rolled try/catch around `Enum.ToObject`.

```csharp
using DKNet.Fw.Extensions.Reflection;

public enum ShippingMethod
{
    Standard = 0,
    Express = 1,
}

public static class EnumConversionExample
{
    public static void Convert()
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

**Notes**: `TryConvertToEnum` converts the incoming value to the enum's underlying integral type via `Convert.ChangeType`, then `Enum.ToObject`s it — it does **not** parse a member-name string. `1` and the numeric string `"1"` both convert to `ShippingMethod.Express`; the member-name string `"Express"` does not (use the BCL `Enum.TryParse` for that). Only `InvalidCastException`, `FormatException`, and `OverflowException` from the conversion are caught and turned into a `false` return with `result = null`; any other exception propagates.

## Runtime behaviour

- **`GetPropertyValue`/`SetPropertyValue`/`TrySetPropertyValue`**: `GetProperty(type, name, flags)` first checks a static `ConcurrentDictionary<(Type, string, BindingFlags), PropertyInfo?>`; on a miss it calls `Type.GetProperty(name, flags)` once and caches the result (including a cached `null` for "not found"). `GetPropertyValue` splits the path on `.` and walks each segment through `GetProperty` + `PropertyInfo.GetValue`, stopping as soon as any intermediate value is `null` or any segment resolves to no property. `SetPropertyValue(PropertyInfo, object?)` special-cases `null` (calls `SetValue(obj, null)`), `Nullable<T>` destinations (unwraps via `Nullable.GetUnderlyingType` then `Convert.ChangeType`), and `enum` destinations (`Enum.Parse` on `value.ToString()`); everything else goes through `Convert.ChangeType(value, propertyType, CultureInfo.CurrentCulture)`.
- **`IsImplementOf`**: short-circuits to `false` for `type == matching`; otherwise tries `matching.IsAssignableFrom(type)` first (covers non-generic interfaces/base classes), then — if `matching` is an interface — scans `type.GetInterfaces()` for an exact open-generic-definition match or an assignable match; otherwise walks `type.BaseType` up the chain looking for a base whose open generic definition equals `matching`.
- **`TypeExtractor` enumeration**: each call to a filter method (`Classes()`, `IsInstanceOf<T>()`, `Where(...)`, ...) does not touch reflection — it only appends a compiled `Func<Type,bool>` to an immutable copy of the predicate list and returns a new `TypeExtractor`. Reflection happens only when the result is enumerated: `GetEnumerator()` resolves each assembly's types through a static `ConcurrentDictionary<Assembly, Type[]>` cache (`asm.GetTypes()` runs once per assembly, ever, for the process lifetime), then applies every accumulated predicate via LINQ `.Where(...)` in the order the filters were chained.
- **`TryConvertToEnum`**: validates `enumType.IsEnumType()` up front (throws `ArgumentException` otherwise, including for a plain non-enum `Type`), then unwraps `Nullable<TEnum>` via `GetNonNullableType()`, converts the incoming value to the enum's declared underlying integral type with `Convert.ChangeType(..., CultureInfo.InvariantCulture)`, and materializes the enum with `Enum.ToObject`. `InvalidCastException`, `FormatException`, and `OverflowException` from the conversion are caught and turned into a `false` return with `result = null`; any other exception propagates. The generic `TryConvertToEnum<TEnum>` overload delegates to the non-generic one via `typeof(TEnum)`.

## Diagnostics & exceptions

| Exception type | Severity | When | Fix |
|---|---|---|---|
| `ArgumentException` | Error (thrown) | `TypeExtractor` constructed with a null or empty `Assembly[]` (via any `Extract()` overload). | Pass at least one assembly. |
| `ArgumentException` | Error (thrown) | `TryConvertToEnum(this Type enumType, ...)` called with an `enumType` that is not an enum (or nullable-enum) type. | Only call it with an actual enum `Type`. |
| `ArgumentException` | Error (thrown) | `SetPropertyValue(string propertyName, object value)` called with a `propertyName` that does not resolve to a property on the target's type. | Verify the property exists, or use `TrySetPropertyValue` if a miss should be tolerated. |
| `ArgumentNullException` | Error (thrown) | `IsNullableType(Type)`, `IsNumericType(Type)`, `SetPropertyValue`/`TrySetPropertyValue` (both overloads) called with a `null` `type`/`obj`/`property`; `SetPropertyValue(string, object)` called with a `null` **or empty** `propertyName`; `TrySetPropertyValue(string, object)` called with a `null` `propertyName` only (an *empty*, non-null `propertyName` throws `ArgumentException` there instead — see the row below); `ExtractDigits()` called on a `null` string. | Null-check inputs before calling, or catch at the boundary if the input is genuinely untrusted. |
| `ArgumentException` | Error (thrown) | `TrySetPropertyValue(string propertyName, object value)` called with a non-null but empty `propertyName` — the guard is `ArgumentException.ThrowIfNullOrEmpty(propertyName)`, whose documented behavior throws `ArgumentNullException` for `null` but `ArgumentException` for empty. | Pass a non-empty property name, or null/empty-check yourself before calling. |
| `FormatException` (swallowed) | n/a — caught internally | `TrySetPropertyValue(PropertyInfo, object?)`'s underlying conversion fails with a format error. | Not surfaced to the caller by design; call `SetPropertyValue` directly if the failure must be observable. |

No Roslyn analyzer or `DiagnosticDescriptor` exists in this package — it ships no source generator and no analyzer.

## Gotchas

- **`PropertyExtensions.GetProperty` *is* cached.** A static `ConcurrentDictionary<(Type, string, BindingFlags), PropertyInfo?>` backs every lookup, including the cached `null` for a not-found property. Do not add your own caching layer on top expecting raw, uncached reflection cost.
- **`TypeExtractor` also caches — per-assembly `GetTypes()`, not per-query.** A static `ConcurrentDictionary<Assembly, Type[]>` means `asm.GetTypes()` runs once per assembly for the process lifetime; only the predicate `.Where(...)` chain re-runs on every enumeration. If an assembly is modified at runtime (unlikely, but relevant to dynamic/collectible assemblies), the cached type list will not reflect that.
- **`IsImplementOf(type, type)` (same type on both sides) returns `false`.** It checks "implements/inherits", not "is". A caller expecting `type.IsImplementOf(type) == true` will silently get the wrong branch of an `if`.
- **`EnumExtensions.GetEnumInfo()` (instance) can produce a `null` `Name` despite `EnumInfo.Name` being `required string`.** It assigns `Name = att?.Name!`, with no field-name fallback, unlike the static `GetEnumInfos<T>()` which does fall back. A caller trusting the non-nullable annotation and indexing into `Name` (e.g. `.Length`) on an enum value without `[Display(Name=...)]` gets a runtime `NullReferenceException`, not a compile error.
- **`string.IsNumber()` is a permissive heuristic, not `decimal.TryParse`.** It only enforces "at most one `.`", "no repeated `,,`", and "any `-` is at index 0". It accepts both `"123,456.789"` (US) and `"123.456,789"` (EU) as "looks numeric", and it rejects a bare `"123-456"` or a trailing-dash string. It is not a substitute for real numeric parsing.
- **`SetPropertyValue`/`TrySetPropertyValue` swallow *different* exception types depending on overload.** The `string`-keyed overload swallows `ArgumentNullException`/`ArgumentException`; the `PropertyInfo`-keyed overload swallows `ArgumentNullException`/`FormatException`. A conversion failure that raises `InvalidCastException` (or anything else) still propagates from either overload.
- **`DateTime.LastDayOfMonth()` preserves `Kind`.** The current implementation (`date.AddDays(...)`) keeps the input's `Kind`, `Hour`/`Minute`/`Second`/`Millisecond` — a `Utc` input stays `Utc`. Do not assume it forces `DateTimeKind.Local`, and do not add a manual `DateTime.SpecifyKind` "fix" after calling it.
- **No flat `DKNet.Fw.Extensions` namespace.** Every type lives in a per-concern namespace (`.Primitives`, `.Reflection`, `.Enums`, `.Collections`, `.TypeExtractors`), except `ServiceCollectionExtensions`/`ServiceCollectionRegistrationExtensions`, which deliberately stay in the ambient `Microsoft.Extensions.DependencyInjection` namespace. `using DKNet.Fw.Extensions;` alone resolves nothing.
- **`GetProperty`'s trimming annotation implies real AOT risk.** The method carries an `IL2075` trim-warning suppression, and the project sets `IsTrimmable=false` in its own `.csproj`. Reflection-by-name lookups in this package are not trim-safe; do not assume a consumer can flip `PublishTrimmed=true` and keep this package's dynamic property access working.
- **`IsKeyedImplementationOf` compares the service key by reference, not equality.** `ReferenceEquals(descriptor.ServiceKey, keyName)` — pass the exact key object used at registration, not merely an equal one.

## Anti-patterns & hallucination traps

- **`IAsyncEnumerable<T>.ToListAsync()` is *not* a DKNet method anymore** — it was removed (it used to live in the ambient `System.Collections.Generic` namespace and collided with .NET 10's own `System.Linq.AsyncEnumerable.ToListAsync`). Use the BCL version instead: `using System.Linq;` then `await source.ToListAsync(cancellationToken)` (returns `List<T>`, takes a `CancellationToken` DKNet's never did).
- **There is no `.Quarter()` method** — it is `InQuarter()`. A plausible-sounding `date.Quarter()` will not compile.
- **`services.Register<T>()` / `services.HasRegistered<T>()` do not exist** — the real names are `IsRegistered<TService>()` and `IsRegisteredWithImplementation<TService>(Type)`.
- **`typeof(X).Implements(typeof(Y))` does not exist** — the real name is `IsImplementOf`, and it is defined on `Type`/`Type?`, not on an instance.
- **`assembly.GetTypes().Extract()` is backwards** — `Extract()` is the entry point *into* the scanner (called on `Assembly`/`Assembly[]`/`ICollection<Assembly>`), not a LINQ-style method called after you already have a `Type[]`.
- **Calling a `TypeExtractor` filter method does not mutate the extractor you called it on** — `var x = assemblies.Extract().Classes(); x.Abstract();` does **not** turn `x` into "abstract classes"; `Abstract()` returns a *new* extractor and `x` remains "all classes". Assign the result: `var abstractOnes = x.Abstract();`.
- **`GetCustomAttribute<T>()` (the BCL `System.Reflection.CustomAttributeExtensions` method) is not the same thing as `HasAttribute<T>()`** — both exist and do different things; check the `using` before assuming a `GetCustomAttribute`/`HasAttribute` call site is a DKNet API.
- **`new TypeExtractor(...)` cannot be constructed directly by a consumer** — `TypeExtractor` is `internal`. The only way in is `Extract()`. Code against `ITypeExtractor` from outside this assembly must go through `Extract()`, never `new TypeExtractor(...)`.
- **Hand-rolling a `GetTypes().Where(t => ...)` scan instead of `TypeExtractors`** loses the assembly-level `GetTypes()` cache, the lazy/non-mutating filter chain, and the open-generic-aware `IsImplementOf` semantics used by `IsInstanceOf<T>()` — prefer the fluent API for anything beyond a single trivial filter.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.RandomCreator` | The other Core package. Reach for it when the need is a cryptographically secure random string/character, not a reflection helper — `DKNet.Fw.Extensions` has no random-generation surface. |
| `DKNet.EfCore.Extensions` | The first-line consumer: uses `Type.IsImplementOf<T>()` in entity-type configuration (e.g. detecting audited-properties/concurrency-entity markers) and `Extract().Classes().NotAbstract().IsInstanceOf<T>()` to auto-discover model builders and data seeders across assemblies. Reach for `DKNet.EfCore.Extensions` when the convention you want applied is an EF Core model convention, not a raw reflection call. |
| `DKNet.AspCore.Idempotency` | Uses `IServiceCollection.IsRegistered<T>()` as its DI duplicate-setup guard — a worked example of that guard in a real registration path. Does not call `PropertyExtensions.GetProperty(...)` (or any other reflection helper here) anywhere in its current source, despite what older material may claim. |
| `DKNet.SlimBus.Extensions` | Does not reference this package directly — picks it up transitively through `DKNet.EfCore.Events` → `DKNet.EfCore.Hooks`. |

## Testing notes

- Pure in-memory reflection/string logic — no containers, no database, no fixtures needed. Plain `[Fact]`/`[Theory]` unit tests against small POCOs are the right shape (xUnit + Shouldly is the DKNet convention; use whatever your own repo already uses).
- For `PropertyExtensions`: construct a small class exposing public/private/protected/nullable/enum members, then exercise `GetProperty`/`GetPropertyValue`/`SetPropertyValue`/`TrySetPropertyValue` for case-insensitivity, dotted nested paths, and every exception overload combination.
- For `TypeExtractor`: assert both the count and a `TrueForAll(...)` against the type's own reflection properties (`t.IsClass`, `t.IsAbstract`, ...) rather than a hardcoded name list, since the exact type roster of a real assembly is what is under test. Also worth a dedicated test: branching one shared base extractor into two filters (e.g. `.Abstract()` and `.NotAbstract()`) must not cross-contaminate the branches.
- For `EnumExtensions.GetEnumInfos<T>()`: back a test enum with a non-`int` underlying type (e.g. `byte`) to prove the compiler-generated `value__` field is excluded regardless of the enum's underlying integral type.
- For `IsNumber()`/`ExtractDigits()`: build a literal input table covering US/EU decimal-separator conventions and mixed-separator edge cases rather than a handful of ad hoc cases — the boundary behavior (repeated commas, dash position, dot count) is easy to under-test.
