# DKNet.Fw.Extensions

`DKNet.Fw.Extensions` is the framework-agnostic, dependency-light base library for the DKNet
suite. It has no domain or infrastructure dependencies (it only pulls in
`Microsoft.Extensions.DependencyInjection.Abstractions` and
`System.ComponentModel.Annotations`), which is why it sits at the innermost layer and is safe to
reference from anything — a domain model, an EF Core configuration, an ASP.NET Core middleware,
or a plain console app.

Reach for it when you find yourself hand-rolling: reflection-based property get/set, "does this
type implement X" checks, enum-to-Display-attribute mapping, digit extraction from a formatted
string, or scanning assemblies for types matching a shape. These are small, focused, well-tested
utilities — use them instead of re-implementing the same reflection snippet in every project.

### Install

```bash
dotnet add package DKNet.Fw.Extensions
```

No DI registration, no configuration, no startup wiring is required for the extension methods —
they are static/extension methods, so referencing the package and adding a `using` is the entire
setup. The one exception is the `ServiceCollectionExtensions` feature described below, which you
call explicitly wherever you build up an `IServiceCollection`.

### Features

#### String extensions (`StringExtensions`)

```csharp
using DKNet.Fw.Extensions;

"Price: $123.45".ExtractDigits();   // "123.45"
"99.99".IsNumber();                 // true
"123-456-7890".IsNumber();          // false — more than one '-', not a leading sign
```

- `string.ExtractDigits()` — pulls out digits plus `.`, `,` and `-` characters (handy for cleaning
  a price or a formatted phone/ID string down to its numeric-looking core; it does not strip the
  separators).
- `string.IsNumber()` — a light heuristic check: not null/whitespace, at most one `.`, no `,,`,
  and any `-` must be in position 0 (a leading sign) or absent. It is not a full numeric parser —
  see Gotchas.
- `PropertyInfo?.IsStringOrValueType()` / `Type?.IsStringOrValueType()` — true when the property's
  (or the type's) unwrapped type is `string` or a value type; used to decide whether a value is
  "simple" enough to display/serialize directly.

#### Type extensions (`TypeExtensions`)

```csharp
using DKNet.Fw.Extensions;

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

#### Enum extensions with `Display` attribute info (`EnumExtensions`, `EnumInfo`)

```csharp
using System.ComponentModel.DataAnnotations;
using DKNet.Fw.Extensions;

public enum OrderStatus
{
    [Display(Name = "Pending", Description = "Waiting for processing")]
    Pending,
    Processing,
}

OrderStatus.Pending.GetAttribute<DisplayAttribute>()?.Name; // "Pending"

var info = OrderStatus.Pending.GetEumInfo();
// info!.Key = "Pending", info.Name = "Pending", info.Description = "Waiting for processing"

foreach (var i in EnumExtensions.GetEumInfos<OrderStatus>())
    Console.WriteLine($"{i.Key}: {i.Name}");
// Pending: Pending
// Processing: Processing   (Name falls back to the field name when there's no [Display])
```

- `Enum?.GetAttribute<T>()` — fetches any custom attribute (not just `Display`) from the field
  backing the current enum value; returns `null` if the enum instance is `null` or the attribute
  isn't present.
- `Enum?.GetEumInfo()` — builds a single `EnumInfo` (`Key`, `Name`, `Description`, `GroupName`)
  from the value's `[Display]` attribute. If there's no `[Display]`, `Name` and `Description` come
  back `null` (unlike the static `GetEumInfos<T>()` below, this instance method does **not**
  fall back to the field name).
- `EnumExtensions.GetEumInfos<T>()` (static, `where T : Enum`) — enumerates every named value of
  enum `T` as an `EnumInfo`, using the field name as `Name` when no `[Display(Name=...)]` is set.
  It skips the compiler-generated `value__` backing field automatically.

`EnumInfo.Name` is declared `required string Name` (non-nullable), but `GetEumInfo()` assigns it
via `att?.Name!` with no fallback — if the enum value has no `[Display]` attribute (or the
attribute has no `Name`), `Name` comes back `null` at runtime despite the non-nullable
declaration. `GetEumInfos<T>()` doesn't have this problem because it falls back to the field
name. Null-check `Name` after calling `GetEumInfo()`.

Note the method names are `GetEumInfo`/`GetEumInfos` (missing the "n") — that's the real,
published API; don't "fix" the typo when calling it.

#### DateTime extensions (`DateTimeExtensions`)

```csharp
using DKNet.Fw.Extensions;

DateTime.Today.InQuarter();          // 1, 2, 3, or 4
DateTime.Today.LastDayOfMonth();     // e.g. 2026-08-31, Kind = Local
((DateTime?)null).LastDayOfMonth();  // null
```

- `DateTime.InQuarter()` — the calendar quarter (1–4) for the date's `Month`.
- `DateTime.LastDayOfMonth()` / `DateTime?.LastDayOfMonth()` — the last calendar day of the same
  month, preserving hour/minute/second/millisecond. The result's `Kind` is always forced to
  `DateTimeKind.Local`, regardless of the input's `Kind` — see Gotchas.

#### Async enumerable extensions (`AsyncEnumerableExtensions`)

```csharp
using System.Collections.Generic; // note: not DKNet.Fw.Extensions

IAsyncEnumerable<int> source = GetAsyncNumbers();
IList<int> all = await source.ToListAsync();
```

- `IAsyncEnumerable<T>.ToListAsync()` — buffers an async sequence into an `IList<T>`. This type is
  deliberately declared in the `System.Collections.Generic` namespace (not `DKNet.Fw.Extensions`),
  so it shows up for any file that already has `using System.Collections.Generic;` — no extra
  `using` needed. Throws `ArgumentNullException` if the source sequence is `null`.

#### Property extensions (`PropertyExtensions`)

```csharp
using DKNet.Fw.Extensions;

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

#### Attribute extensions (`AttributeExtensions`)

```csharp
using DKNet.Fw.Extensions;

typeof(Product).HasAttribute<ObsoleteAttribute>();                 // Type overload
productType.GetProperty("Price").HasAttribute<RequiredAttribute>(); // PropertyInfo overload
product.HasAttributeOnProperty<RequiredAttribute>("Price");         // by property name, via reflection
```

- `PropertyInfo?.HasAttribute<TAttribute>(bool inherit = true)` and
  `Type?.HasAttribute<TAttribute>(bool inherit = true)` — null-safe `Attribute.IsDefined` checks.
- `object.HasAttributeOnProperty<TAttribute>(string propertyName, bool inherit = true)` — resolves
  the property by name (via `PropertyExtensions.GetProperty`, case-insensitive) and then checks
  for the attribute; returns `false` if the property doesn't exist.

#### Collection extensions (`CollectionExtensions`)

```csharp
using DKNet.Fw.Extensions;

ICollection<int> target = [1, 2, 3];
target.AddRange([4, 5, 6]); // target now has 6 items
```

- `ICollection<T>.AddRange(IEnumerable<T> items)` — adds every item from `items` one at a time
  (there's no bulk/`List<T>.AddRange`-style fast path; it's a plain `foreach` + `Add`).

#### Service-collection / DI extensions (`ServiceCollectionExtensions`, `ServiceCollectionRegistrationExtensions`)

These are inspection/guard helpers you call while assembling an `IServiceCollection` — they don't
register anything themselves.

```csharp
using Microsoft.Extensions.DependencyInjection;
using DKNet.Fw.Extensions; // brings the "extension" members into scope

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

#### `TypeExtractors` — fluent assembly/type scanning

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

### Configuration

There is nothing to configure. No options object, no `IOptions<T>`, no environment-specific
behavior — every member above is a static or extension method with fixed behavior. The only
"configuration" surface is the `BindingFlags` parameter you can optionally pass to
`GetProperty(...)`.

### How it composes with other DKNet packages

`DKNet.Fw.Extensions` sits at the bottom of the dependency graph and is referenced directly by:

- `DKNet.EfCore.Extensions` — uses `Type.IsImplementOf<T>()` in entity-type configuration (e.g.
  detecting `IAuditedProperties`/`IConcurrencyEntity<>`) and `TypeExtractors` (`Extract().Classes().NotAbstract().IsInstanceOf<T>()`)
  to auto-discover model builders and data seeders across assemblies.
- `DKNet.AspCore.Idempotency` — uses `IServiceCollection.IsRegistered<T>()` as a duplicate-setup
  guard in its DI registration extension, plus `PropertyExtensions.GetProperty(...)`.

`DKNet.SlimBus.Extensions` does not reference this package directly; it picks it up transitively
through `DKNet.EfCore.Events` → `DKNet.EfCore.Hooks`.

### Gotchas and limits

- **`GetEumInfos`/`GetEumInfo` naming** — the real, published method names are missing the "n"
  (`GetEumInfo`, not `GetEnumInfo`). There is no `Quarter()` method either — it's `InQuarter()`.
  Older docs/blog snippets that use `GetEnumInfo()` or `.Quarter()` will not compile against this
  package.
- **`string.IsNumber()` is a heuristic, not a parser** — it accepts things like `"1,234"` or
  `"1.2.3"`-adjacent edge cases are only partially guarded (it only checks there's at most one
  `.`, no `,,`, and any `-` is at position 0). Don't use it as a substitute for
  `decimal.TryParse`/`double.TryParse` when you need correctness, only for a quick "looks numeric"
  filter.
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
- **`AsyncEnumerableExtensions.ToListAsync` lives in `System.Collections.Generic`**, not
  `DKNet.Fw.Extensions` — that's deliberate (so it's picked up without an extra `using`), but it
  means you won't find it by browsing the `DKNet.Fw.Extensions` namespace in IntelliSense.
