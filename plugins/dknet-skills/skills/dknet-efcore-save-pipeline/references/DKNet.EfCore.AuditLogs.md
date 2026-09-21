# DKNet.EfCore.AuditLogs

| Field | Value |
|---|---|
| Area | EfCore |
| NuGet | `dotnet add package DKNet.EfCore.AuditLogs` |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.AuditLogs.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/EfCore/DKNet.EfCore.AuditLogs |
| Depends on (DKNet) | `DKNet.EfCore.Abstractions`, `DKNet.EfCore.Hooks` |
| Depends on (3rd party) | `Microsoft.EntityFrameworkCore` (version centrally pinned) |
| Target framework | net10.0 |

## Purpose

A `SaveChanges` interceptor (`EfCoreAuditHook`, registered as an `IHookAsync` hook on `DKNet.EfCore.Hooks`' pipeline) that walks every tracked `Added`/`Modified`/`Deleted` entry implementing `IAuditedProperties`, diffs its mapped scalar properties into an `AuditLogEntry`, and hands the batch to one or more `IAuditLogPublisher` implementations you supply. Sensitive-looking values (name-pattern match, `SecureString`, `[SensitiveData]`, or an attribute literally named `EncryptedAttribute`) are redacted to `"***REDACTED***"` before publishing.

It is **not** a change-tracking debug tool (use `DbContext.ChangeTracker.DebugView` for that) and it does **not** encrypt or protect values at rest (use `DKNet.EfCore.Encryption` for that) — it only decides what appears in the audit trail.

## Entry points

| Call | Signature | Notes |
|---|---|---|
| `AddEfCoreAuditHook<TDbContext>` | `IServiceCollection AddEfCoreAuditHook<TDbContext>(this IServiceCollection services, AuditLogBehaviour behaviour = AuditLogBehaviour.IncludeAllAuditedEntities, AuditPropertyPolicy propertyPolicy = AuditPropertyPolicy.RedactSensitive) where TDbContext : DbContext` | Registers a singleton `IOptions<AuditLogOptions>` and calls `services.AddHook<TDbContext, EfCoreAuditHook>()`. Does **not** attach the hook to the `DbContext` pipeline by itself — the `DbContext` must additionally be registered via `AddDbContextWithHook<TDbContext>` (or `options.UseHooks<TDbContext>(provider)`), or the hook silently never runs. |
| `AddEfCoreAuditLogs<TDbContext, TPublisher>` | `IServiceCollection AddEfCoreAuditLogs<TDbContext, TPublisher>(this IServiceCollection services, AuditLogBehaviour behaviour = AuditLogBehaviour.IncludeAllAuditedEntities, AuditPropertyPolicy propertyPolicy = AuditPropertyPolicy.RedactSensitive) where TDbContext : DbContext where TPublisher : class, IAuditLogPublisher` | Registers `TPublisher` as `AddKeyedScoped<IAuditLogPublisher, TPublisher>(typeof(TDbContext).FullName!)`, then calls `AddEfCoreAuditHook<TDbContext>(behaviour, propertyPolicy)`. No-ops (skips the keyed registration and the passed `behaviour`/`propertyPolicy`) if that exact `TPublisher`+`TDbContext` pair is already registered. |
| `AddCurrentUserProvider<TDbContext, TProvider>` | `IServiceCollection AddCurrentUserProvider<TDbContext, TProvider>(this IServiceCollection services) where TDbContext : DbContext where TProvider : class, ICurrentUserProvider` | Registers `TProvider` as an **un-keyed, application-wide** `AddScoped<ICurrentUserProvider, TProvider>()` — first caller wins. Registers default `AuditLogOptions` only if none exists yet. Always calls `AddHook<TDbContext, EfCoreAuditHook>()` for the named `TDbContext`, so it works standalone (stamping only, no publisher). |
| `GetAuditLogPublishers<TDbContext>` | `IEnumerable<IAuditLogPublisher> GetAuditLogPublishers<TDbContext>(this IServiceProvider provider) where TDbContext : DbContext` | `provider.GetKeyedServices<IAuditLogPublisher>(typeof(TDbContext).FullName)` — resolves whatever was keyed-registered for that `DbContext` type; useful in tests or manual invocation. Never throws for a missing key — returns empty. |
| `[AuditLog]` | Class or property, `Inherited = false` (`DKNet.EfCore.Abstractions.Attributes`) | Class: required for the entity under `AuditLogBehaviour.OnlyAttributedAuditedEntities`. Property: forces plaintext capture past the sensitive-name deny-list under `RedactSensitive`, and allow-lists the property under `OnlyAttributedProperties` — never overrides `[SensitiveData]` or `[Encrypted]` on the same property. |
| `[IgnoreAuditLog]` | Class or property, `Inherited = false` (`DKNet.EfCore.Abstractions.Attributes`) | Class: entity produces no `AuditLogEntry` at all, regardless of behaviour. Property: that property never appears in `Changes`, redacted or not. |
| `[SensitiveData]` | Property only (`DKNet.EfCore.Abstractions.Attributes`), ctor `(params string[] roles)` | Always redacted in every audit entry, unconditionally — wins even over `[AuditLog]` on the same property. `Roles` is for role-gated API response filtering elsewhere; ignored by this package. |
| `[Encrypted]` (`DKNet.EfCore.Encryption.Attributes.EncryptedAttribute`) | Property | Matched by **attribute type name** `"EncryptedAttribute"`, not type identity. Redacted unconditionally, exactly like `[SensitiveData]`. This package takes no project/package reference on `DKNet.EfCore.Encryption` — any attribute type named `EncryptedAttribute`, from any assembly, triggers it. |

`AddEfCoreAuditHook`/`AddEfCoreAuditLogs`/`AddCurrentUserProvider` are declared as C# `extension(IServiceCollection services)` members on `EfCoreAuditLogSetup`, in namespace `DKNet.EfCore.AuditLogs` — **not** ambient into `Microsoft.Extensions.DependencyInjection`. `using DKNet.EfCore.AuditLogs;` is required.

## Public surface

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `AuditFieldChange` | sealed record | One field-level diff inside an `AuditLogEntry`. | `required string FieldName { get; init; }`; `object? OldValue { get; init; }`; `object? NewValue { get; init; }` |
| `AuditLogAction` | enum | The high-level operation an entry records. | `Created`, `Updated`, `Deleted` |
| `AuditLogEntry` | sealed record, implements `IAuditedProperties` | One captured create/update/delete of an audited entity. | `required AuditLogAction Action { get; init; }`; `required DateTimeOffset CreatedOn { get; init; }`; `DateTimeOffset? UpdatedOn { get; init; }`; `required IDictionary<string, object?> Keys { get; init; }`; `required IReadOnlyList<AuditFieldChange> Changes { get; init; }`; `required string CreatedBy { get; init; }`; `required string EntityName { get; init; }`; `string? UpdatedBy { get; init; }` |
| `AuditLogBehaviour` | enum | Entity-level gate. | `IncludeAllAuditedEntities` (default), `OnlyAttributedAuditedEntities` |
| `AuditPropertyPolicy` | enum | Property-level gate. | `RedactSensitive` (default), `OnlyAttributedProperties` |
| `IAuditLogPublisher` | interface | Extension point: where entries go. | `Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)` |
| `ICurrentUserProvider` | interface | Extension point: who the change is attributed to. | `string? GetCurrentUser()` |

`AuditLogOptions` (the options object backing the two DI parameters) and the internal `EfCoreAuditHook` hook, `AuditPropertyStamper`, and `SensitiveDataPatterns` helpers are all `internal` — mentioned in Runtime behaviour and Gotchas below because they explain observable behaviour, but not directly usable from application code.

## Options & defaults

| Option | Default | Effect |
|---|---|---|
| `behaviour` | `IncludeAllAuditedEntities` | `IncludeAllAuditedEntities`: audits every `IAuditedProperties` entity not marked `[IgnoreAuditLog]`. `OnlyAttributedAuditedEntities`: audits only entities marked `[AuditLog]` at class level. |
| `propertyPolicy` | `RedactSensitive` | `RedactSensitive`: captures every non-ignored property, redacting sensitive-looking ones. `OnlyAttributedProperties`: captures only `[AuditLog]`-marked properties, omits the rest. |

Both values are fixed for the whole application at DI-registration time — there is no post-registration options object to mutate and no per-save/per-entity override. `AddCurrentUserProvider<TDbContext, TProvider>()` registers these same two defaults only when no `AuditLogOptions` is registered yet, so it never overwrites a prior `AddEfCoreAuditHook`/`AddEfCoreAuditLogs` call's choice, and the two calls may run in either order.

## Usage patterns

### Wire the hook, a publisher, and current-user stamping

**When**: standard app startup — audit every `IAuditedProperties` entity on `AppDbContext` and ship entries to a custom sink, attributing changes to the signed-in user.

```csharp
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public interface ICurrentPrincipal
{
    string? SubjectId { get; }
}

public sealed class SignedInUserProvider(ICurrentPrincipal principal) : ICurrentUserProvider
{
    public string? GetCurrentUser() => principal.SubjectId; // e.g. "sub-8f21c0"
}

public sealed class ConsoleAuditLogPublisher : IAuditLogPublisher
{
    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
    {
        foreach (var log in logs)
        {
            var fields = string.Join(", ", log.Changes.Select(c => $"{c.FieldName}: {c.OldValue} -> {c.NewValue}"));
            Console.WriteLine($"[{log.Action}] {log.EntityName} by {log.UpdatedBy ?? log.CreatedBy} -- {fields}");
        }
        return Task.CompletedTask;
    }
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options);

public static class Startup
{
    public static void ConfigureServices(IServiceCollection services, string connectionString)
    {
        // 1. Register the DbContext through the hook-aware overload -- mandatory, or the hook never runs.
        services.AddDbContextWithHook<AppDbContext>((provider, options) =>
            options.UseSqlite(connectionString));

        // 2. Register the audit hook plus a publisher, keyed to AppDbContext.
        services.AddEfCoreAuditLogs<AppDbContext, ConsoleAuditLogPublisher>();

        // 3. Optional: fill CreatedBy/UpdatedBy from the signed-in user.
        services.AddCurrentUserProvider<AppDbContext, SignedInUserProvider>();
    }
}
```

**Notes**: `AppDbContext`'s entities must implement `IAuditedProperties` (directly, or via `AuditedEntity`/`AuditedEntity<TKey>` from `DKNet.EfCore.Abstractions`) to be audited at all — a plain `DbContext` entity is always skipped. Step 3 can appear before or after step 2; neither overwrites the other's `AuditLogOptions`.

### Register more than one publisher for the same DbContext

**When**: both a console sink and a database sink should see the same audit trail.

```csharp
using DKNet.EfCore.AuditLogs;
using Microsoft.Extensions.DependencyInjection;

public sealed class DatabaseAuditLogPublisher : IAuditLogPublisher
{
    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public static class MultiPublisherStartup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddEfCoreAuditHook<AppDbContext>(); // hook only, no publisher yet
        services.AddKeyedScoped<IAuditLogPublisher, ConsoleAuditLogPublisher>(typeof(AppDbContext).FullName!);
        services.AddKeyedScoped<IAuditLogPublisher, DatabaseAuditLogPublisher>(typeof(AppDbContext).FullName!);
    }
}
```

**Notes**: both publishers are invoked for every save that produces entries; one throwing does not stop the other (see Diagnostics & exceptions). Resolve them yourself with `serviceProvider.GetAuditLogPublishers<AppDbContext>()`.

### Restrict auditing to an explicit allow-list

**When**: an opt-in model — nothing audited unless a developer marks it.

```csharp
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.AuditLogs;
using Microsoft.Extensions.DependencyInjection;

[AuditLog] // required under OnlyAttributedAuditedEntities
public sealed class ApiClient : AuditedEntity<Guid>
{
    public required string Name { get; set; }

    [AuditLog] // name matches the "token" pattern, but this forces plaintext capture / allow-lists it
    public DateTimeOffset TokenExpiryUtc { get; set; }

    [SensitiveData] // always redacted, regardless of name or [AuditLog]
    public string? InternalNotes { get; set; }

    [IgnoreAuditLog] // never appears in Changes at all
    public byte[]? Thumbnail { get; set; }
}

public static class AllowListStartup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddEfCoreAuditLogs<AppDbContext, ConsoleAuditLogPublisher>(
            behaviour: AuditLogBehaviour.OnlyAttributedAuditedEntities,
            propertyPolicy: AuditPropertyPolicy.OnlyAttributedProperties);
    }
}
```

**Notes**: under `OnlyAttributedProperties`, `Name` and `Thumbnail` never appear in `Changes` at all — only `TokenExpiryUtc` (allow-listed by `[AuditLog]`) is captured, and it is captured in plaintext because `[AuditLog]` at property level also forces past the name-pattern redactor.

### Protect a value at rest and in the audit trail together

**When**: a property is both encrypted via `DKNet.EfCore.Encryption` and must never appear in the audit trail.

```csharp
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Encryption.Attributes;

public sealed class Customer : AuditedEntity<Guid>
{
    public required string Name { get; set; }

    [Encrypted] // encrypted at rest, and always redacted in the audit trail (matched by attribute name)
    public string? TaxId { get; set; }
}
```

**Notes**: this package takes no reference on `DKNet.EfCore.Encryption` — redaction fires because the redaction engine matches any attribute whose CLR type name is exactly `EncryptedAttribute`. Installing `DKNet.EfCore.Encryption` (see `dknet-efcore-data-security`) is what makes `TaxId` actually encrypted at rest; this package only decides it never appears in plaintext in the audit trail.

### Read publishers manually / assert in a test

**When**: verifying wiring, or invoking publishers outside a live save.

```csharp
using DKNet.EfCore.AuditLogs;

public static class PublisherReader
{
    public static async Task PublishAllAsync(IServiceProvider serviceProvider, IEnumerable<AuditLogEntry> someEntries)
    {
        var publishers = serviceProvider.GetAuditLogPublishers<AppDbContext>();
        foreach (var publisher in publishers)
        {
            await publisher.PublishAsync(someEntries);
        }
    }
}
```

**Notes**: returns whatever is keyed-registered under `typeof(AppDbContext).FullName` — empty if nothing was registered for that `DbContext` type; it never throws for a missing key.

## Runtime behaviour

For one `SaveChangesAsync()` call on a `DbContext` registered with the hook pipeline:

1. **Before-save**: stamping runs first — if a registered `ICurrentUserProvider.GetCurrentUser()` returns a non-empty value, it stamps `CreatedBy`/`CreatedOn` for every `Added` entry (only if `CreatedBy` is still empty — first write wins) and `UpdatedBy`/`UpdatedOn` for every `Modified` entry (skipped if a domain method, e.g. `SetUpdatedBy(...)`, already changed those fields in this change set). Then, for every tracked entry that is `Added`, `Modified`, or `Deleted`, it builds an `AuditLogEntry` by reflecting over `[IgnoreAuditLog]`, `[AuditLog]`, `[SensitiveData]`, and the `EncryptedAttribute`-by-name check for that entity type (cached per CLR type for the process lifetime). Entities that are not `IAuditedProperties`, or excluded by `[IgnoreAuditLog]`/the configured `behaviour`, yield nothing. The resulting entries are cached per `DbContext` instance.
2. EF Core executes the actual database write.
3. **After-save** (only once the write has succeeded): retrieves and removes the cached entries for this `DbContext` instance, then awaits every keyed `IAuditLogPublisher.PublishAsync(...)` in turn, each inside its own try/catch.

For `Created` entities the field-diff loop never runs, so `Changes` is always empty for a create. For `Deleted` entities every captured property gets `NewValue = null` and `OldValue` set to the last known value (or the redaction sentinel).

## Diagnostics & exceptions

| Exception | When | Fix |
|---|---|---|
| `ArgumentException` | Stamping `CreatedBy`/`UpdatedBy`/`CreatedOn`/`UpdatedOn` and the property is neither part of the EF model nor found as a writable CLR property anywhere in the entity's type hierarchy. | Ensure the audited entity actually declares/maps those properties (e.g. inherit `AuditedEntity<TKey>`); this should not occur for entities implementing `IAuditedProperties` normally. |
| Logged `Error` (not thrown) | A registered `IAuditLogPublisher.PublishAsync` throws. The log message names the publisher type, distinct entity names, and entry count — never field values. | Fix the publisher; the save has already committed and cannot be rolled back, so this cannot recover the dropped audit entries. Use a transactional outbox if at-least-once delivery is required. |
| `ObjectDisposedException` | Accessing the snapshot's `DbContext`/`Entities` after it was disposed — inherited from `DKNet.EfCore.Extensions.Snapshots`, not specific to this package. | Don't retain a `SnapshotContext` reference past the hook call that received it. |

There are no analyzer `DiagnosticDescriptor`s in this package.

## Gotchas

- **`CreatedBy`/`UpdatedBy`/`CreatedOn`/`UpdatedOn` are NOT excluded from `Changes` by default**, despite `IAuditedProperties` declaring all four `[IgnoreAuditLog]` at the interface level — .NET attribute lookup does not propagate an attribute from an interface member to the implementing class's member. `AuditedEntity<TKey>`'s own `UpdatedBy`/`UpdatedOn` carry no attribute of their own, so they show up as ordinary changed fields. Add `[IgnoreAuditLog]` on your own subclass's properties, or filter them out in your `IAuditLogPublisher`, if you need them hidden.
- **Registering the hook is not enough.** `AddEfCoreAuditHook`/`AddEfCoreAuditLogs` only add DI registrations; without `AddDbContextWithHook<TDbContext>` (or a manual `options.UseHooks<TDbContext>(provider)`), the interceptor is never attached and the hook silently never runs — no exception, no log.
- **`AddCurrentUserProvider` never overwrites a prior `AuditLogOptions`** — its default is registered only when no `AuditLogOptions` is registered yet.
- **`ICurrentUserProvider` is application-wide, not per-`DbContext`, and first registration wins.** A second call with a different provider silently keeps the first one and only attaches the hook to the new `TDbContext`.
- **Registering the same publisher type twice for the same DbContext is a silent no-op**, including any different `behaviour`/`propertyPolicy` passed on the second call.
- **Redaction hides the value, not the fact of a change** — both `OldValue` and `NewValue` become the identical sentinel string `"***REDACTED***"`, so a redacted field can no longer signal whether the new value actually differs from the old.
- **Publishing is awaited inside `SaveChangesAsync`, not fire-and-forget** — a slow `IAuditLogPublisher` adds directly to save latency.
- **A publisher exception is swallowed after the write has already committed** — there is no recovery path for a dropped entry.
- **`[AuditLog]` at property level never wins over `[SensitiveData]` or `[Encrypted]`-by-name on the same property** — an `[AuditLog][SensitiveData]` property is still redacted.
- **The current-user identifier is published to every `IAuditLogPublisher` completely unmasked** — redaction rules apply only to entity property values, never to `CreatedBy`/`UpdatedBy` themselves. Return a stable, non-personal id (e.g. a token subject id) if under a personal-data rule.
- **Per-entity-type audit plans are cached for the process lifetime** — attributes are assumed static and are never re-evaluated after the first save of a given entity type.

## Anti-patterns & hallucination traps

- There is **no public/mutable `AuditLogOptions` class you can inject or reconfigure post-startup** — the only knobs are the two constructor-time parameters on `AddEfCoreAuditHook`/`AddEfCoreAuditLogs`. Do not write `services.Configure<AuditLogOptions>(...)` — it compiles against nothing public and is not a supported pattern here.
- **There is no public `BuildAuditLog`/`EfCoreAuditHook` API to call directly** — both are `internal`. DI (`AddEfCoreAuditHook`/`AddEfCoreAuditLogs`) is the only supported entry point.
- **Do not call `SaveChangesAsync()` from inside `IAuditLogPublisher.PublishAsync`** on the same `DbContext` that is mid-save — `PublishAsync` runs after the write has committed; re-entering the same save pipeline is not supported.
- **`[IgnoreAuditLog]` does not disable auditing for the whole `DbContext`** — it is per entity class or per property; there is no DbContext-wide opt-out attribute.
- **`AddEfCoreAuditHook`/`AddEfCoreAuditLogs` do not themselves call `AddDbContextWithHook`** — a common mistake is assuming the audit registration wires the interceptor; it does not (see Gotchas).
- **`ICurrentUserProvider` is not the same thing as `DKNet.EfCore.DataAuthorization`'s `IDataOwnerProvider`** — they are independent interfaces with independent registration; registering one does not satisfy the other.
- **The built-in sensitive-name deny-list (`password`, `token`, `apikey`, …) is fixed and not configurable** — there is no options entry to add/remove name fragments; the extension points are `[SensitiveData]` (add) and `[AuditLog]` at property level (opt a name-matched property back into plaintext, but never past `[SensitiveData]`/`[Encrypted]`).

## Composes with

| Package | Rule |
|---|---|
| `DKNet.EfCore.Hooks` | Required — supplies the `SaveChanges` before/after pipeline (`IHookAsync`, `AddHook<TDbContext,THook>`, `AddDbContextWithHook<TDbContext>`) that `EfCoreAuditHook` plugs into. Reach for it directly to write a custom hook. |
| `DKNet.EfCore.Abstractions` | Required — supplies `IAuditedProperties`, `AuditedEntity`/`AuditedEntity<TKey>`, and `[AuditLog]`/`[IgnoreAuditLog]`/`[SensitiveData]`. Reach for it to make an entity auditable at all. |
| `DKNet.EfCore.Events` | Reach for it when the system must *react* to a change (domain events) rather than just record it; runs on the same hook pipeline, ordered by DI registration order alongside this hook. |
| `DKNet.EfCore.Encryption` | Reach for it to protect a value at rest; this package only decides whether the (already-encrypted) value appears in an audit entry, matched by the `EncryptedAttribute` type name. |
| `DKNet.EfCore.DataAuthorization` | Reach for it for row-level ownership filtering. Its `DataOwnerHook` shares internal stamping helpers with this package and supplies the ownership-key fallback for `CreatedBy`/`UpdatedBy` when no `ICurrentUserProvider` value is available for a save. |

## Testing notes

- Tests call the `internal` `BuildAuditLog(...)` extension directly against a hand-built `EntityEntry` for pure capture-logic assertions (their test project has `InternalsVisibleTo` on the package) — no hook/DI plumbing needed for that layer.
- End-to-end hook tests build a small `ServiceCollection`, call `AddEfCoreAuditLogs<TDbContext, TestPublisher>()` (or `AddEfCoreAuditHook` plus a keyed publisher) plus `AddDbContextWithHook<TDbContext>`, then drive real `SaveChangesAsync()` calls.
- **SQLite (not EF Core InMemory) backs these DbContext-level tests**, with a fresh temp file per test and `EnableSensitiveDataLogging()` where value assertions matter — consistent with the repo-wide "never use EF Core InMemory for integration tests" rule, applied here even for hook-only unit tests. No TestContainers.MsSql — no server-specific SQL is involved.
- A per-instance, DI-scoped collector (not a shared static one) is the canonical test-double publisher — tests specifically guard against a shared static collector leaking entries between concurrent test classes.
- Redaction assertions compare against the package's own redaction sentinel constant. A logger test double captures output to assert a publisher-failure error log never contains raw field values, only entity/publisher names and counts.
