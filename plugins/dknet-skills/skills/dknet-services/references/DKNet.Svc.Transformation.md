# DKNet.Svc.Transformation reference

| Field | Value |
|---|---|
| Area | Services |
| NuGet | `dotnet add package DKNet.Svc.Transformation` |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.Transformation.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/Services/DKNet.Svc.Transformation |
| Depends on (DKNet) | none — no `ProjectReference` in the `.csproj` |
| Depends on (3rd party) | `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Options` |
| Target framework | `net10.0` |

## Purpose

Fills bracketed tokens (`[Name]`, `{Email}`, `<Amount>`, `{{Ref}}`, or a custom pair) inside a template string by
extracting each token with an `ITokenDefinition`, resolving its value by case-insensitive reflection/dictionary
lookup against one or more data sources, formatting the value through a pluggable `IValueFormatter`, and rebuilding
the string in a single pass. It is **not** an object mapper and **not** a template engine — there is no dotted/nested
property syntax (`[Customer.Name]` does not resolve), and no conditionals, loops, or partials; reach for Razor,
Scriban, or Handlebars instead if a template needs logic.

## Entry points

| Call | Exact signature | Called on | Notes (lifetime, ordering, prerequisites) |
|---|---|---|---|
| `AddTransformerService` | `public static IServiceCollection AddTransformerService(this IServiceCollection services, Action<TransformOptions>? optionFactory = null)` | `IServiceCollection` (ambient namespace `Microsoft.Extensions.DependencyInjection`) | Idempotent: returns immediately if `ITransformerService` is already registered, so a **second** call's `optionFactory` is silently ignored — the first call's options win. Registers `IOptions<TransformOptions>` as a singleton and `ITransformerService → TransformerService` as transient. No `IConfiguration`/`appsettings.json` binding path exists for this package. |

## Public surface

### `DKNet.Svc.Transformation`

| Type | Kind | Purpose | Key members with exact signatures |
|---|---|---|---|
| `ITransformerService` | interface | Consumer-facing transform contract | `string Transform(string templateString, params object[] parameters);` `Task<string> TransformAsync(string templateString, params object[] parameters);` |
| `TransformerService` | sealed class (implements `ITransformerService`) | Default implementation, constructed by DI | `public TransformerService(IOptions<TransformOptions> options)` (primary-constructor class, **not** a record); `public string Transform(string templateString, params object[] parameters)`; `public async Task<string> TransformAsync(string templateString, params object[] parameters)` |
| `TransformOptions` | class | Root options object | See **Options & defaults** below |
| `TokenNotFoundBehavior` | enum | Governs unresolved-token behavior | `LeaveAsIs`, `Remove`, `ThrowError` (default) |

### `DKNet.Svc.Transformation.TokenExtractors`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IToken` | interface | A single extracted token occurrence | `int Index { get; }`; `ITokenDefinition Definition { get; }`; `string Key { get; }` (tag-stripped name); `string OriginalString { get; }`; `string Token { get; }` (raw text incl. tags) |
| `ITokenDefinition` | interface | Bracket-pair contract | `string BeginTag { get; }`; `string EndTag { get; }`; `bool IsToken(string value);` |
| `TokenDefinition` | sealed class (implements `ITokenDefinition`) | Concrete begin/end tag pair | `public TokenDefinition(string begin, string end)` — throws `ArgumentException` if either tag is null/whitespace; `bool IsToken(string value)` — true only when `value` starts with `BeginTag`, ends with `EndTag` (both ordinal-ignore-case), and the inner payload is non-empty and contains no character from either tag |
| `ITokenExtractor` | interface | Extraction contract — **no public implementation exists**; the only implementation is `internal sealed` | `IReadOnlyCollection<IToken> Extract(string templateString);` `Task<IReadOnlyCollection<IToken>> ExtractAsync(string templateString);` |
| `ITokenResolver` | interface | Resolution contract — **no public implementation exists**; the only implementation is `internal sealed` | `object? Resolve(IToken token, params object?[] data);` `Task<object?> ResolveAsync(IToken token, params object?[] data);` |

You receive `IToken` instances from the built-in extractor; you never construct one yourself — the concrete
`TokenResult` type is `internal sealed` and throws `InvalidTokenException` in its constructor if the token text
doesn't validate against its `ITokenDefinition`.

### `DKNet.Svc.Transformation.Convertors`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IValueFormatter` | interface | Value→display-string contract | `string Convert(IToken token, object value);` |
| `ValueFormatter` | class (implements `IValueFormatter`) | Default formatter, culture-invariant | `public virtual string DateFormat { get; set; } = "dd/MM/yyyy hh.mm.ss";` `public virtual string IntegerFormat { get; set; } = "###,##0";` `public virtual string NumberFormat { get; set; } = "###,##0.00";` `public virtual string Convert(IToken token, object? value)` — `bool`→`"Yes"`/`"No"`; `int`/`long`→`IntegerFormat`; `double`/`decimal`/`float`→`NumberFormat`; `DateTime`/`DateTimeOffset`→`DateFormat`; everything else→`ToString()`; `null`→`string.Empty`. All numeric/date formatting uses `CultureInfo.InvariantCulture`. |

### `DKNet.Svc.Transformation.Exceptions`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `InvalidTokenException` | sealed class : `Exception` | Thrown when a token is built from text its `ITokenDefinition` rejects | `public InvalidTokenException(string token, Exception? innerException = null)` |
| `UnResolvedTokenException` | sealed class : `Exception` | Thrown when `TokenNotFoundBehavior.ThrowError` (default) and a token resolves to `null` | `public UnResolvedTokenException(IToken token, Exception? innerException = null)` |

### `Microsoft.Extensions.DependencyInjection` (ambient namespace)

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `TransformSetup` | static class | DI registration entry point | `public static IServiceCollection AddTransformerService(this IServiceCollection services, Action<TransformOptions>? optionFactory = null)` |

## Options & defaults

| Option | Type | Default | Effect | Where set |
|---|---|---|---|---|
| `DefaultDefinitions` | `ICollection<ITokenDefinition>` (get-only) | `[SquareBrackets]` | Bracket styles recognized by the extractors built at `TransformerService` construction. Add-only through the property (no setter) — `Clear()` first to drop `SquareBrackets`. | `TransformOptions` |
| `Formatter` | `IValueFormatter` | `new ValueFormatter()` | Converts each resolved value to its display string. | `TransformOptions` |
| `GlobalParameters` | `IEnumerable<object>` | `[]` | Fallback resolution sources, tried after the call's own `parameters`. | `TransformOptions` |
| `TokenNotFoundBehavior` | `TokenNotFoundBehavior` | `ThrowError` | `LeaveAsIs` keeps token text; `Remove` substitutes `string.Empty`; `ThrowError` throws `UnResolvedTokenException`. | `TransformOptions` |

Built-in `static readonly ITokenDefinition` fields on `TransformOptions`: `SquareBrackets` (`"["`,`"]"`), `CurlyBrackets`
(`"{"`,`"}"`), `AngledBrackets` (`"<"`,`">"`), `DoubleCurlyBrackets` (`"{{"`,`"}}"`).

## Usage patterns

### Register and inject the default service

**When**: standard app startup, one bracket style is enough.

```csharp
using DKNet.Svc.Transformation;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddTransformerService(); // ITransformerService, transient
```

Inject it into a consumer:

```csharp
using DKNet.Svc.Transformation;

public sealed class WelcomeEmailBuilder(ITransformerService transformer)
{
    public Task<string> BuildAsync(object user) =>
        transformer.TransformAsync("Hello [Name], your account [Email] is ready.", user);
}
```

**Notes**: `AddTransformerService()` with no delegate uses every `TransformOptions` default (square brackets,
`ThrowError`, invariant-culture `ValueFormatter`). Calling it a second time anywhere in the app is a no-op.

### Transform against a plain object

**When**: the data source is a DTO or anonymous object; property lookup is case-insensitive.

```csharp
using DKNet.Svc.Transformation;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddTransformerService();
var provider = services.BuildServiceProvider();
var transformer = provider.GetRequiredService<ITransformerService>();

var order = new { OrderId = 4711, Total = 129.5m, Placed = new DateTime(2026, 3, 1), Paid = true };
var text = transformer.Transform("Order [OrderId] — [Total] on [Placed]. Paid: [Paid]", order);
// "Order 4,711 — 129.50 on 01/03/2026 12.00.00. Paid: Yes"
```

**Notes**: `int`/`decimal` use `IntegerFormat`/`NumberFormat`; `DateTime` uses `DateFormat`
(`"dd/MM/yyyy hh.mm.ss"`, 12-hour with no meridiem); `bool` renders `Yes`/`No`.

### Transform against a string dictionary

**When**: the data is already `IDictionary<string, string>` (e.g. form fields, query params).

```csharp
using DKNet.Svc.Transformation;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddTransformerService();
var transformer = services.BuildServiceProvider().GetRequiredService<ITransformerService>();

var model = new Dictionary<string, string> { { "Name", "John Doe" }, { "Location", "DKNet" } };
var result = transformer.Transform("Hello [Name], welcome to [Location]!", model);
// "Hello John Doe, welcome to DKNet!"
```

**Notes**: the dictionary must be exactly `IDictionary<string, string>` — any other `IDictionary`, including
`Dictionary<string, object>`, throws `ArgumentException` during resolution.

### Enable extra bracket styles and lenient unresolved tokens

**When**: the template mixes bracket styles, or missing data should not throw.

```csharp
using DKNet.Svc.Transformation;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddTransformerService(options =>
{
    options.DefaultDefinitions.Add(TransformOptions.CurlyBrackets);
    options.DefaultDefinitions.Add(TransformOptions.AngledBrackets);
    options.TokenNotFoundBehavior = TokenNotFoundBehavior.LeaveAsIs;
});
var transformer = services.BuildServiceProvider().GetRequiredService<ITransformerService>();

var rs = transformer.Transform("[Name] / {Email} / <Missing>", new { Name = "Duy", Email = "duy@example.com" });
// "Duy / duy@example.com / <Missing>"
```

**Notes**: `DefaultDefinitions` already contains `SquareBrackets`; the property has no setter, so `Clear()` first if
you want to drop it. Enabling both `CurlyBrackets` and `DoubleCurlyBrackets` at once is ambiguous — the same text can
match twice and be substituted twice.

### Custom value formatter and global parameters

**When**: dates must render unambiguously, and some values (company name, support email) are shared by every
template in the app.

```csharp
using DKNet.Svc.Transformation.Convertors;

public sealed class IsoDateFormatter : ValueFormatter
{
    public override string DateFormat { get; set; } = "yyyy-MM-dd";
}
```

Wire it up as `Formatter`, alongside a global parameter every template can see:

```csharp
using DKNet.Svc.Transformation;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddTransformerService(options =>
{
    options.Formatter = new IsoDateFormatter();
    options.GlobalParameters = [new { CompanyName = "Acme Corp" }];
});
var transformer = services.BuildServiceProvider().GetRequiredService<ITransformerService>();

var rs = transformer.Transform("From [CompanyName], dated [Today]", new { Today = new DateTime(2026, 3, 1) });
// "From Acme Corp, dated 2026-03-01"
```

**Notes**: resolution tries the call's own `parameters` first, then falls back to `GlobalParameters`; with no
`parameters` at all, only `GlobalParameters` is used.

### Custom bracket pair

**When**: the app's templates also pass through another templating layer, so square/curly/angle brackets are taken.

```csharp
using DKNet.Svc.Transformation;
using DKNet.Svc.Transformation.TokenExtractors;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddTransformerService(options =>
{
    options.DefaultDefinitions.Clear();
    options.DefaultDefinitions.Add(new TokenDefinition("@(", ")"));
});
var transformer = services.BuildServiceProvider().GetRequiredService<ITransformerService>();

var rs = transformer.Transform("Hello @(name)!", new Dictionary<string, string> { { "name", "John Doe" } });
// "Hello John Doe!"
```

**Notes**: `new TokenDefinition(begin, end)` throws `ArgumentException` for a null/whitespace tag. A candidate only
counts as a token when its inner text is non-empty and contains no character from either tag.

## Runtime behaviour

For `Transform`/`TransformAsync`, in order:

1. **Construction-time (once):** `TransformerService`'s field initializer builds one token extractor per entry in
   `TransformOptions.DefaultDefinitions` at the time the service is constructed. Mutating `DefaultDefinitions`
   afterward has no effect on an already-constructed instance.
2. **Extraction:** `Transform` calls each extractor's synchronous `Extract`; `TransformAsync` calls each
   extractor's `ExtractAsync` and awaits all of them together. Each extractor scans the template with `Span<char>`,
   skips a begin tag immediately followed by another begin tag (so back-to-back openers do not nest), and
   validates every candidate against `ITokenDefinition.IsToken` before wrapping it as a token.
3. **Ordering + per-call cache:** the resolved tokens are sorted by `Index`, and a fresh dictionary scoped to that
   single `Transform`/`TransformAsync` call is created — never persisted on the instance, so it cannot leak a
   value between two calls, only deduplicate a token repeated within the same template.
4. **Resolution:** for each token, the per-call cache is checked first, then the internal resolver is tried
   against the call's own `parameters` (if any), then against `TransformOptions.GlobalParameters`. Resolution
   branches per data item: `IDictionary` → cast to `IDictionary<string, string>` (throws otherwise) and look up
   the key case-insensitively; `IEnumerable<object?>` → recurse item by item; anything else → reflection lookup of
   a public instance property (case-insensitive), falling back to a non-public instance property. First non-null
   value wins.
5. **Not-found policy:** a `null` result falls through to `TransformOptions.TokenNotFoundBehavior` — `LeaveAsIs`
   (token text verbatim), `Remove` (empty string), or `ThrowError` (throws `UnResolvedTokenException`, the default).
6. **Formatting + rebuild:** the resolved (or policy-substituted) value is passed to `Options.Formatter.Convert`
   along with the token, and the result is appended to the output alongside the untouched template text between
   tokens.

## Diagnostics & exceptions

| Exception type | Severity | When | Fix |
|---|---|---|---|
| `UnResolvedTokenException` | Runtime exception | `TokenNotFoundBehavior.ThrowError` (default) and a token's resolved value is `null` (missing property or a property that itself holds `null` — the two cannot be distinguished) | Ensure every token has a non-null value in `parameters`/`GlobalParameters`, or switch `TokenNotFoundBehavior` to `LeaveAsIs`/`Remove` |
| `InvalidTokenException` | Runtime exception | A token is built from text its `ITokenDefinition.IsToken` returns false for — only reachable if a custom `ITokenExtractor` builds one from unvalidated text (the built-in extractor always validates first) | Validate with `ITokenDefinition.IsToken` before constructing a token |
| `ArgumentException` | Runtime exception | A dictionary parameter is not `IDictionary<string, string>` (e.g. `Dictionary<string, object>`); or `TokenDefinition(begin, end)` is given a null/whitespace tag | Pass `IDictionary<string, string>`; use non-empty tag strings |
| `ArgumentNullException` | Runtime exception | `TransformerService(IOptions<TransformOptions> options)` with `options.Value == null`; resolving with a `null` token; constructing an extractor with a `null` definition | Pass non-null arguments (mainly reachable only through direct, non-DI construction) |
| `ArgumentOutOfRangeException` | Runtime exception | A token constructed with an out-of-range index | Only reachable via a custom `ITokenExtractor`; keep indices within the template's bounds |

No analyzer `DiagnosticDescriptor`s exist in this package — it is runtime-only, no Roslyn analyzer/generator.

## Gotchas (source-verified)

- **The per-token cache is scoped to a single `Transform`/`TransformAsync` call, not to the `TransformerService`
  instance.** A fresh cache dictionary is created on every call; it only deduplicates a token repeated within one
  template, and cannot leak a value from one call into another — reusing one instance across two different data
  sets is safe.
- **Extractors are captured once at construction, not rebuilt per call.** `TransformerService`'s extractor list
  is built once from `options.Value.DefaultDefinitions` when the service is constructed; adding a definition to
  the same `TransformOptions` instance afterward is invisible to that already-constructed service instance.
- **A dictionary parameter must be exactly `IDictionary<string, string>`.** The internal token resolver casts to
  that interface and throws `ArgumentException` for anything else, including `Dictionary<string, object>`.
- **Non-public properties are readable.** The internal resolver falls back to non-public instance properties when
  no public property matches — a template can pull a value the type does not expose publicly; don't feed
  entities with sensitive internal state into a user-authored template.
- **No nested/dotted property paths.** `[Customer.Name]` is looked up as a single literal property named
  `Customer.Name`, which normally fails and falls through to `TokenNotFoundBehavior` — flatten the data first.
- **Back-to-back begin tags are consumed one layer at a time, not treated as escaping.** The internal extractor
  skips ahead when a begin tag is immediately followed by another begin tag, so `"Hoang [[A]] Duy"` with
  `A = "Bao"` becomes `"Hoang [Bao] Duy"` — the outer bracket is not preserved as a literal `[`.
- **`DefaultDefinitions` has no setter and starts non-empty.** It is a get-only `ICollection<ITokenDefinition>`
  seeded with `SquareBrackets`; to use only custom definitions, `Clear()` it first.
- **`AddTransformerService` is registered idempotently, per-app not per-call.** `TransformSetup.AddTransformerService`
  checks whether `ITransformerService` is already registered before doing anything else — a second call anywhere
  in the composition root silently drops its `optionFactory`.
- **`ITokenExtractor` and `ITokenResolver` are public interfaces with no injection seam.** Their only
  implementations are `internal sealed`, and `TransformerService` constructs them itself — implementing either
  interface compiles but is never called.

## Anti-patterns & hallucination traps

- `ICustomConverter`, `TransformationException`, a `DisabledLocalCache` option, and template syntax like
  `{amount:currency:USD}` **do not exist anywhere in source** — do not suggest them.
- `[Customer.Name]` / any dotted or bracketed nested-property syntax — not supported; resolution is a flat,
  single-property reflection lookup.
- Constructing the internal extractor/resolver types from consumer code — both are `internal sealed`; they will
  not even compile outside the package and its test project.
- Passing `Dictionary<string, object>` (or any `IDictionary` other than `IDictionary<string, string>`) as a
  parameter — looks reasonable, throws `ArgumentException` at runtime.
- Registering `ITransformerService` as a singleton "to avoid rebuilding the resolver" — unnecessary; the per-call
  cache already means there is no correctness reason to avoid a singleton, and the package registers it `Transient`
  by design in `AddTransformerService`.
- Expecting `TransformOptions` to bind from `appsettings.json`/`IConfiguration` — there is no such binding path; it is
  configured only through the `Action<TransformOptions>?` delegate passed to `AddTransformerService`.
- Hand-rolling per-call string formatting instead of subclassing `ValueFormatter` or assigning
  `TransformOptions.Formatter` — the formatter is the documented seam for that.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.Svc.PdfGenerators` | Fill a Markdown/HTML template with `ITransformerService`, then convert the filled result to PDF. |
| `DKNet.Svc.BlobStorage.Abstractions` | Read the template string from blob storage before transforming it, or store the rendered output afterward. See `dknet-blob-storage`. |
| (none, structurally) | This package has no `ProjectReference` to any other DKNet package — it depends only on `Microsoft.Extensions.DependencyInjection.Abstractions`/`Microsoft.Extensions.Options`, so it is safe to call from a domain service, a message handler, or a background worker without pulling in EF Core or SlimBus. |

## Testing notes

Plain xUnit + Shouldly (plus a few plain `Assert.Equal`/`Should.ThrowAsync` calls) — no TestContainers, no DB, no
HTTP host. Two patterns cover the whole surface:

- **Through DI:** build a `ServiceCollection`, call `AddTransformerService(o => ...)`, `BuildServiceProvider()`,
  and resolve `ITransformerService` — used for registration-idempotency and `TokenNotFoundBehavior` scenarios.
- **Direct construction:** `new TransformerService(Options.Create(new TransformOptions { ... }))`, bypassing
  DI — used whenever a test needs to control exactly what `IOptions<TransformOptions>` the instance sees, e.g.
  the cache-scoping and extractor-capture tests.

Other patterns worth reusing in your own tests: a helper that `Clear()`s `DefaultDefinitions` before adding
specific ones so a test's bracket set is deterministic; a spy formatter/provider with a side-effecting property
getter to assert the per-call cache deduplicates a repeated token exactly once; and a large-template round-trip
test that asserts none of `{`, `[`, `<` survive substitution, to prove every token across three bracket styles was
replaced.
