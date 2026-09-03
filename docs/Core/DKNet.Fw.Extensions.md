# DKNet.Fw.Extensions

Framework-agnostic reflection, type, string, enum and DI-inspection helpers shared by every layer of a
DKNet solution.

## ✨ Why use it?

- **Safe to reference from anywhere** – it pulls in only
  `Microsoft.Extensions.DependencyInjection.Abstractions` and `System.ComponentModel.Annotations`, so a
  domain model, an EF Core configuration, an ASP.NET Core middleware and a plain console app can all take
  it without dragging infrastructure along. That is why it sits at the innermost layer of the suite.
- **Stop re-writing the same reflection snippet** – get/set a property by name (including dotted paths
  such as `"Owner.Address.City"`), check for an attribute, or read a `[Display]` attribute off an enum
  value, once here instead of once per project.
- **Type-shape checks that understand open generics** – `Type.IsImplementOf(typeof(IRepository<>))`
  answers "does this implement that shape" across interfaces, base classes and generic definitions;
  a hand-written `IsAssignableFrom` call does not.
- **Readable assembly scanning** –
  `assemblies.Extract().Classes().NotAbstract().IsInstanceOf<IEventHandler>()` replaces a hand-rolled
  `GetTypes().Where(...)` chain, and is evaluated lazily.
- **Duplicate-registration guards for DI** – `IsRegistered<T>()` and
  `IsRegisteredWithImplementation<T>(...)` let a library extension method stay idempotent without
  inspecting `ServiceDescriptor`s by hand.

Reach for it whenever you catch yourself hand-rolling reflection-based property access, a
"does this type implement X" check, enum-to-`Display` mapping, digit extraction from a formatted string,
or an assembly scan for types matching a shape.

## 🚀 Quick Start

```bash
dotnet add package DKNet.Fw.Extensions
```

```csharp
using DKNet.Fw.Extensions.Reflection;

var product = new Product { Name = "Laptop", Price = 999.99m };

var name = product.GetPropertyValue("name");                  // "Laptop" — lookup is case-insensitive
product.SetPropertyValue("Price", 1099.99m);                  // converted to the property's type

typeof(List<string>).IsImplementOf(typeof(IEnumerable<>));    // true — open generic match
```

No DI registration, no configuration and no startup wiring is required for the extension methods — they are
static/extension methods, so referencing the package and adding the right `using` is the entire setup. The
one exception is the `ServiceCollectionExtensions` feature described below, which you call explicitly
wherever you build up an `IServiceCollection`.

Members are grouped into per-area namespaces rather than one flat `DKNet.Fw.Extensions` namespace — see
the `using` line on each example below.

## 🧩 Features

### String extensions (`StringExtensions`)

```csharp
using DKNet.Fw.Extensions.Primitives;

"Price: $123.45".ExtractDigits();   // "123.45"
"99.99".IsNumber();                 // true
"123-456-7890".IsNumber();          // false — more than one '-', not a leading sign
```

- `string.ExtractDigits()` — pulls out digits plus `.`, `,` and `-` characters (handy for cleaning
  a price or a formatted phone/ID string down to its numeric-looking core; it does not strip the
  separators).
- `string.IsNumber()` — a light heuristic check: not null/whitespace, at most one `.`, no `,,`,
  and any `-` must be in position 0 (a leading sign) or absent. It is not a full numeric parser —
  see Gotchas. It deliberately does not enforce `,`/`.` ordering or count, so it accepts both the
  US convention (`"123,456.789"` — comma thousands separator, dot decimal) and the European one
  (`"123.456,789"` — dot thousands separator, comma decimal) as "looks numeric". This is
  intentional: `decimal.TryParse` under `InvariantCulture` only accepts one of those conventions,
  so `IsNumber()` fills a different, more permissive role.
- `PropertyInfo?.IsStringOrValueType()` / `Type?.IsStringOrValueType()` — true when the property's
  (or the type's) unwrapped type is `string` or a value type; used to decide whether a value is
  "simple" enough to display/serialize directly.

### Type extensions (`TypeExtensions`)

```csharp
using DKNet.Fw.Extensions.Reflection;

typeof(int?).GetNonNullableType();          // typeof(int)
typeof(List<string>).IsImplementOf(typeof(IEnumerable<>)); // true (open generic match)
typeof(MyRepo<User>).IsImplementOf<IRepository<User>>();   // true
typeof(decimal).IsNumericType();            // true
typeof(MyEnum).TryConvertToEnum(1, out var value); // true, value = (MyEnum)1
```

- `Type.GetNonNullableType()` — unwraps `Nullable<T>` to `T` (returns the type unchanged if it
  isn't nullable).
- `Type.IsAssignableFrom<TType>()` / `Type.IsAssignableTo<TType>()` — generic-friendly wrappers
  around the reflection `IsAssignableFrom`/`IsAssignableTo`.
- `Type?.IsEnumType()` — true for `enum` types and `Nullable<SomeEnum>`.
- `Type?.IsImplementOf(Type? matching)` / `Type.IsImplementOf<T>()` — the workhorse type-shape
  check used throughout DKNet: true if the type implements an interface (including an open
  generic interface definition such as `IRepository<>`), inherits a base class, or matches a
  generic base definition anywhere up the hierarchy. Returns `false` when `type == matching`
  (it checks *implements/inherits*, not *is*).
- `Type.IsNumericType()` / `object?.IsNumericType()` — true for the built-in integer, floating
  point, and `decimal` types (nullable-aware).
- `Type.TryConvertToEnum(object value, out object? result)` / `object.TryConvertToEnum<TEnum>(out TEnum? result)`
  — converts a raw value (e.g. a boxed `int` from a database or JSON) into the target enum,
  returning `false` instead of throwing on `InvalidCastException`, `FormatException`, or
  `OverflowException`. Throws `ArgumentException` up front if the non-generic overload's
  `enumType` isn't actually an enum.

### Enum extensions with `Display` attribute info (`EnumExtensions`, `EnumInfo`)

```csharp
using System.ComponentModel.DataAnnotations;
using DKNet.Fw.Extensions.Enums;

public enum OrderStatus
{
    [Display(Name = "Pending", Description = "Waiting for processing")]
    Pending,
    Processing,
}

OrderStatus.Pending.GetAttribute<DisplayAttribute>()?.Name; // "Pending"

var info = OrderStatus.Pending.GetEnumInfo();
// info!.Key = "Pending", info.Name = "Pending", info.Description = "Waiting for processing"

foreach (var i in EnumExtensions.GetEnumInfos<OrderStatus>())
    Console.WriteLine($"{i.Key}: {i.Name}");
// Pending: Pending
// Processing: Processing   (Name falls back to the field name when there's no [Display])
```

- `Enum?.GetAttribute<T>()` — fetches any custom attribute (not just `Display`) from the field
  backing the current enum value; returns `null` if the enum instance is `null` or the attribute
  isn't present.
- `Enum?.GetEnumInfo()` — builds a single `EnumInfo` (`Key`, `Name`, `Description`, `GroupName`)
  from the value's `[Display]` attribute. If there's no `[Display]`, `Name` and `Description` come
  back `null` (unlike the static `GetEnumInfos<T>()` below, this instance method does **not**
  fall back to the field name).
- `EnumExtensions.GetEnumInfos<T>()` (static, `where T : Enum`) — enumerates every named value of
  enum `T` as an `EnumInfo`, using the field name as `Name` when no `[Display(Name=...)]` is set.
  It skips the compiler-generated `value__` backing field automatically.

`EnumInfo.Name` is declared `required string Name` (non-nullable), but `GetEnumInfo()` assigns it
via `att?.Name!` with no fallback — if the enum value has no `[Display]` attribute (or the
attribute has no `Name`), `Name` comes back `null` at runtime despite the non-nullable
declaration. `GetEnumInfos<T>()` doesn't have this problem because it falls back to the field
name. Null-check `Name` after calling `GetEnumInfo()`.

### DateTime extensions (`DateTimeExtensions`)

```csharp
using DKNet.Fw.Extensions.Primitives;

DateTime.Today.InQuarter();          // 1, 2, 3, or 4
DateTime.Today.LastDayOfMonth();     // e.g. 2026-08-31, Kind = Local
((DateTime?)null).LastDayOfMonth();  // null
```

- `DateTime.InQuarter()` — the calendar quarter (1–4) for the date's `Month`.
- `DateTime.LastDayOfMonth()` / `DateTime?.LastDayOfMonth()` — the last calendar day of the same
  month, preserving hour/minute/second/millisecond. The result's `Kind` is always forced to
  `DateTimeKind.Local`, regardless of the input's `Kind` — see Gotchas.

### Async enumerable extensions — removed

`AsyncEnumerableExtensions.ToListAsync(this IAsyncEnumerable<T>)` has been **removed**. It used to live
in the ambient `System.Collections.Generic` namespace precisely so it would show up without an extra
`using` — but that same ambient placement is what killed it: .NET 10 ships its own
`System.Linq.AsyncEnumerable.ToListAsync` extension in that same reachable surface, and having both in
scope made every call site ambiguous. Use the BCL method instead:

```csharp
using System.Linq; // System.Linq.AsyncEnumerable.ToListAsync

IAsyncEnumerable<int> source = GetAsyncNumbers();
List<int> all = await source.ToListAsync(cancellationToken);
```

The BCL version also takes a `CancellationToken` (DKNet's did not) and returns `List<T>` rather than
`IList<T>`.

### Property extensions (`PropertyExtensions`)

```csharp
using DKNet.Fw.Extensions.Reflection;

var product = new Product { Name = "Laptop", Price = 999.99m };

var prop = product.GetProperty("name");          // case-insensitive, any access level
var value = product.GetPropertyValue("Owner.Address.City"); // dotted path, nested properties

product.SetPropertyValue("Price", 1099.99m);     // by name, converts to the property's type
product.SetPropertyValue(prop!, 42);             // by PropertyInfo
product.TrySetPropertyValue("DoesNotExist", 1);  // swallows the failure instead of throwing
```

- `T?.GetProperty(string propertyName, BindingFlags flags = IgnoreCase | Public | NonPublic | Instance)`
  (for any `T : class`, and also works when `obj` is itself a `Type`) — reflection lookup that
  defaults to case-insensitive, all-visibility instance properties.
- `T?.GetPropertyValue(string propertyName)` — like the above but returns the *value*, and
  supports dotted paths (`"Owner.Address.City"`) to walk into nested objects; returns `null` as
  soon as any segment is missing or `null`.
- `object.SetPropertyValue(PropertyInfo property, object? value)` /
  `object.SetPropertyValue(string propertyName, object value)` — sets a property, converting the
  incoming value to the property's type (`Convert.ChangeType`, with special-casing for nullable
  and enum destination types). Throws `ArgumentException` if the named property can't be found.
- `object.TrySetPropertyValue(string propertyName, object value)` /
  `object.TrySetPropertyValue(PropertyInfo property, object? value)` — same as `SetPropertyValue`
  but swallows `ArgumentNullException`/`ArgumentException` (string overload) or
  `ArgumentNullException`/`FormatException` (`PropertyInfo` overload) and writes a `Debug.WriteLine`
  instead of throwing — useful for best-effort mapping where a missing/mismatched field shouldn't
  blow up the whole operation.
- `Type.IsNullableType()` — true only for `Nullable<T>` (not for reference types); throws
  `ArgumentNullException` if `type` is `null`.

### Attribute extensions (`AttributeExtensions`)

```csharp
using DKNet.Fw.Extensions.Reflection;

typeof(Product).HasAttribute<ObsoleteAttribute>();                 // Type overload
productType.GetProperty("Price").HasAttribute<RequiredAttribute>(); // PropertyInfo overload
product.HasAttributeOnProperty<RequiredAttribute>("Price");         // by property name, via reflection
```

- `PropertyInfo?.HasAttribute<TAttribute>(bool inherit = true)` and
  `Type?.HasAttribute<TAttribute>(bool inherit = true)` — null-safe `Attribute.IsDefined` checks.
- `object.HasAttributeOnProperty<TAttribute>(string propertyName, bool inherit = true)` — resolves
  the property by name (via `PropertyExtensions.GetProperty`, case-insensitive) and then checks
  for the attribute; returns `false` if the property doesn't exist.

### Collection extensions (`CollectionExtensions`)

```csharp
using DKNet.Fw.Extensions.Collections;

ICollection<int> target = [1, 2, 3];
target.AddRange([4, 5, 6]); // target now has 6 items
```

- `ICollection<T>.AddRange(IEnumerable<T> items)` — adds every item from `items` one at a time
  (there's no bulk/`List<T>.AddRange`-style fast path; it's a plain `foreach` + `Add`).

### Service-collection / DI extensions (`ServiceCollectionExtensions`, `ServiceCollectionRegistrationExtensions`)

These are inspection/guard helpers you call while assembling an `IServiceCollection` — they don't
register anything themselves.

```csharp
using Microsoft.Extensions.DependencyInjection; // ServiceCollectionExtensions lives in this namespace

// Guard a single-active-implementation contract from being registered twice.
if (!services.IsRegistered<IIdempotencyKeyStore>())
    services.AddSingleton<IIdempotencyKeyStore, SqlIdempotencyKeyStore>();

// Guard a multi-implementation contract from registering the *same* implementation twice,
// while still allowing a second, different implementation to coexist.
if (!services.IsRegisteredWithImplementation<IEventHandler>(typeof(OrderCreatedHandler)))
    services.AddScoped<IEventHandler, OrderCreatedHandler>();

// Inspect a ServiceDescriptor, including keyed registrations.
ServiceDescriptor descriptor = services[0];
bool isFoo = descriptor.IsImplementationOf<IFoo>();
bool isKeyedFoo = descriptor.IsKeyedImplementationOf<IFoo>("fooKey");
```

- `IServiceCollection.IsRegistered<TService>()` — true if *any* implementation of `TService` is
  already registered. Use this as a first-wins guard for a single-active-implementation contract.
- `IServiceCollection.IsRegisteredWithImplementation<TService>(Type implementationType)` — true
  only if that exact `(TService, implementationType)` pair is already registered. Use this to let
  multiple distinct implementations of the same service coexist while still avoiding duplicate
  registration of the same one.
- `ServiceDescriptor.IsImplementationOf(Type implementationType)` / `IsImplementationOf<TImplement>()`
  — true if the descriptor's `ServiceType` or `ImplementationType` matches.
- `ServiceDescriptor.IsKeyedImplementationOf(object keyName, Type implementationType)` /
  `IsKeyedImplementationOf<TImplement>(object keyName)` — true only for a *keyed* service
  descriptor (`IsKeyedService`) whose key reference-equals `keyName` and whose implementation
  matches.

### `TypeExtractors` — fluent assembly/type scanning

```csharp
using System.Reflection;
using DKNet.Fw.Extensions.TypeExtractors;

Assembly[] assemblies = [typeof(Program).Assembly];

var handlerTypes = assemblies
    .Extract()          // ITypeExtractor over every type in the given assemblies
    .Classes()
    .NotAbstract()
    .IsInstanceOf<IEventHandler>()
    .Where(t => t.Namespace!.EndsWith("Handlers", StringComparison.Ordinal))
    .ToList(); // ITypeExtractor : IEnumerable<Type>, so LINQ works directly on it
```

- `Assembly.Extract()`, `Assembly[].Extract()`, `ICollection<Assembly>.Extract()` — entry points
  that wrap one or more assemblies in an `ITypeExtractor`.
- `ITypeExtractor` is a chainable, lazily-evaluated filter builder (`IEnumerable<Type>`) with
  paired positive/negative filters: `Classes()`/`NotClass()`, `Enums()`/`NotEnum()`,
  `Interfaces()`/`NotInterface()`, `Abstract()`/`NotAbstract()`, `Generic()`/`NotGeneric()`,
  `Nested()`/`NotNested()`, `Publics()`/`NotPublic()`, plus `HasAttribute<TAttribute>()` /
  `HasAttribute(Type attributeType)`, `IsInstanceOf(Type?)` / `IsInstanceOf<T>()` /
  `IsInstanceOfAny(params Type[])` / `NotInstanceOf(Type?)` / `NotInstanceOf<T>()`, and a general
  `Where(Expression<Func<Type, bool>>? predicate)` escape hatch. `IsInstanceOf`/`NotInstanceOf`
  are built on `Type.IsImplementOf`, so they match open generic interfaces/base types too.
  Filters are additive (each call narrows further) and only run when you enumerate the result —
  nothing is scanned until you call `ToList()`, `foreach`, etc.
- `TypeExtractor` (the internal implementation behind `ITypeExtractor`) throws
  `ArgumentException` if constructed with a null/empty assembly array; duplicate assemblies passed
  in are de-duplicated automatically.
- **Every filter call returns a new extractor; none of them mutate the one you called them on.**
  Branching off a shared extractor is safe:
  ```csharp
  var classes = assemblies.Extract().Classes();
  var abstractOnes = classes.Abstract();      // does not affect classes
  var concreteOnes = classes.NotAbstract();   // independent branch, sees the full Classes() set
  ```
  `abstractOnes` and `concreteOnes` each see every class, filtered only by their own branch —
  `classes` itself stays reusable as the common ancestor for as many branches as you need.

## ⚙️ Configuration reference

There is no options object, no `IOptions<T>` and no environment-specific behavior — every member above is a
static or extension method with fixed behavior. The only configurable surface is a single optional
parameter:

| Option | Type | Default | Effect |
|---|---|---|---|
| `GetProperty(propertyName, flags)` – `flags` | `BindingFlags` | `IgnoreCase \| Public \| NonPublic \| Instance` | Which properties the reflection lookup considers. `GetPropertyValue`, `SetPropertyValue`, `TrySetPropertyValue` and `HasAttributeOnProperty` all resolve through this default and do not expose the parameter themselves. |
| `HasAttribute<TAttribute>(inherit)` / `HasAttributeOnProperty<TAttribute>(propertyName, inherit)` – `inherit` | `bool` | `true` | Whether attributes inherited from a base type/property count as present. |

## 🧱 Where it fits

`TypeExtractors` is the piece other DKNet packages lean on hardest — it is how they discover your entity
configurations, seeders, and global model builders without you registering each one by hand:

![Data-flow diagram of TypeExtractor: assemblies enter through Extract(), chain through shape filters (Classes, Interfaces, Enums, Abstract) and relationship filters (IsInstanceOf, HasAttribute, Where), and stay lazy until enumerated into the type list that DKNet's entity configuration, seeding, and model-builder discovery consume.](../diagrams/fw-extensions-type-extractor.svg)

`DKNet.Fw.Extensions` sits at the bottom of the dependency graph and is referenced directly by:

- `DKNet.EfCore.Extensions` — uses `Type.IsImplementOf<T>()` in entity-type configuration (e.g.
  detecting `IAuditedProperties`/`IConcurrencyEntity<>`) and `TypeExtractors` (`Extract().Classes().NotAbstract().IsInstanceOf<T>()`)
  to auto-discover model builders and data seeders across assemblies.
- `DKNet.AspCore.Idempotency` — uses `IServiceCollection.IsRegistered<T>()` as a duplicate-setup
  guard in its DI registration extension, plus `PropertyExtensions.GetProperty(...)`.

`DKNet.SlimBus.Extensions` does not reference this package directly; it picks it up transitively
through `DKNet.EfCore.Events` → `DKNet.EfCore.Hooks`.

## ⚠️ Gotchas & limits

- **No `Quarter()` method** — it's `InQuarter()`. Older docs/blog snippets that use `.Quarter()`
  will not compile against this package.
- **`string.IsNumber()` is a heuristic, not a parser** — it accepts things like `"1,234"` or
  `"1.2.3"`-adjacent edge cases are only partially guarded (it only checks there's at most one
  `.`, no `,,`, and any `-` is at position 0). Don't use it as a substitute for
  `decimal.TryParse`/`double.TryParse` when you need correctness, only for a quick "looks numeric"
  filter across both US and European decimal conventions.
- **Reflection-heavy, no caching** — `PropertyExtensions`, `AttributeExtensions`, and
  `TypeExtractors` all do live reflection calls (`GetProperty`, `GetCustomAttributes`,
  `GetTypes()`) on every call/enumeration; nothing is cached internally. Fine for startup-time
  scanning or occasional calls; cache the `PropertyInfo`/`Type` list yourself if you're calling
  these in a hot path.
- **`DateTime.LastDayOfMonth()` forces `DateTimeKind.Local`** — even if the input `DateTime` was
  `Utc` or `Unspecified`, the returned value's `Kind` is always `Local`. Don't rely on it to
  preserve UTC dates.
- **`Type.IsImplementOf` is not "is same type"** — `type.IsImplementOf(type)` returns `false`;
  it answers "does this type implement/inherit something else", not "is this type equal to it".
- **`SetPropertyValue`/`TrySetPropertyValue` differ in exceptions swallowed** — the string-keyed
  overloads swallow `ArgumentNullException`/`ArgumentException`; the `PropertyInfo`-keyed
  overloads swallow `ArgumentNullException`/`FormatException`. A type-conversion failure that
  throws something else (e.g. `InvalidCastException`) will still propagate.
- **There is no flat `DKNet.Fw.Extensions` namespace.** Members live in per-area namespaces —
  `DKNet.Fw.Extensions.Primitives` (string, `DateTime`), `.Reflection` (type, property, attribute),
  `.Enums`, `.Collections`, `.TypeExtractors` — while `ServiceCollectionExtensions` is declared in
  `Microsoft.Extensions.DependencyInjection`. A single `using DKNet.Fw.Extensions;` does not
  compile.
- **`ToListAsync` on `IAsyncEnumerable<T>` is no longer a DKNet method.** Use
  `System.Linq.AsyncEnumerable.ToListAsync` from the BCL (`using System.Linq;`) — see
  [Async enumerable extensions — removed](#async-enumerable-extensions--removed) above.

## 🔗 Related packages

- [DKNet.RandomCreator](./DKNet.RandomCreator.md) – the other Core package; cryptographically secure random
  strings and characters. Reach for it when you need a secret value, not a reflection helper.
- [DKNet.EfCore.Extensions](../EfCore/DKNet.EfCore.Extensions.md) – the first-line consumer of
  `IsImplementOf` and `TypeExtractors`; reach for it when the convention you want applied is an EF Core
  model convention rather than a raw reflection call.
- [DKNet.AspCore.Idempotency](../AspNetCore/DKNet.AspCore.Idempotency.md) – uses the DI registration
  guards and `GetProperty(...)` from this package; a worked example of both in a real registration path.
