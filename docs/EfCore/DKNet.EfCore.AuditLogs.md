# DKNet.EfCore.AuditLogs

A `SaveChanges` interceptor that captures a structured, field-level change record for every created, updated, or
deleted entity, and hands the batch to publishers you register.

## ✨ Why use it?

- **No hand-rolled `ChangeTracker` walk** — the hook enumerates every `Added`/`Modified`/`Deleted` entry and diffs its
  mapped scalar properties for you, instead of that loop being copy-pasted into each `DbContext`.
- **Sensitive values are redacted by default** — property names matching a built-in deny-list (`password`, `token`,
  `apikey`, `ssn`, `creditcard`, …) and any `SecureString` property are captured as `"***REDACTED***"`, so an audit
  trail cannot silently become a credential leak. A property carrying an `[Encrypted]`
  (`DKNet.EfCore.Encryption.Attributes.EncryptedAttribute`) attribute is redacted the same way, unconditionally.
- **Declarative opt-in and opt-out** — `[AuditLog]`, `[IgnoreAuditLog]`, and `[SensitiveData]` on the entity decide
  what is captured, so the policy lives next to the model rather than in audit plumbing.
- **The audit identity is your application's, not the tenant's** — register an `ICurrentUserProvider` and the same
  hook stamps `CreatedBy`/`UpdatedBy` from the signed-in user *before* it captures the entry, so the saved row and
  its audit record always agree on who made the change.
- **You own the sink** — the package produces `AuditLogEntry` records and calls your `IAuditLogPublisher`; where they
  land (table, queue, log sink) is your decision, keyed per `DbContext` type.
- **Shares one pipeline with the other hook packages** — auditing, domain events, and data authorization all run in
  the same before/after-save pass from [DKNet.EfCore.Hooks](./DKNet.EfCore.Hooks.md), not in three competing
  interceptors.

Reach for this package when you need an automatic audit trail of entity changes and are willing to plug in your own
storage. If you only need EF Core's change-tracking debug view for troubleshooting, use
`DbContext.ChangeTracker.DebugView`; if you need to protect the value at rest rather than log its history, use
[DKNet.EfCore.Encryption](./DKNet.EfCore.Encryption.md).

## 🚀 Quick Start

```bash
dotnet add package DKNet.EfCore.AuditLogs
```

The package depends on [DKNet.EfCore.Abstractions](./DKNet.EfCore.Abstractions.md) (for `IAuditedProperties` and the `[AuditLog]`/`[IgnoreAuditLog]`/`[SensitiveData]` attributes) and [DKNet.EfCore.Hooks](./DKNet.EfCore.Hooks.md) (for the `SaveChanges` pipeline), plus `Microsoft.EntityFrameworkCore`.

Registering the hook alone is **not** enough — the `DbContext` must also be wired into the hook pipeline via `AddDbContextWithHook` (or a manual `options.UseHooks<TDbContext>(provider)` call), exactly as for any other `DKNet.EfCore.Hooks` consumer:

```csharp
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.Hooks;
using Microsoft.Extensions.DependencyInjection;

// 1. Register the DbContext through the hook-aware overload.
services.AddDbContextWithHook<AppDbContext>((provider, options) =>
    options.UseSqlServer(connectionString));

// 2. Register the audit hook plus a publisher, keyed to AppDbContext.
services.AddEfCoreAuditLogs<AppDbContext, MyAuditLogPublisher>();

// 3. Optional: fill CreatedBy/UpdatedBy from the signed-in user instead of leaving them to
//    DKNet.EfCore.DataAuthorization's ownership key.
services.AddCurrentUserProvider<AppDbContext, SignedInUserProvider>();
```

`AddEfCoreAuditLogs<TDbContext, TPublisher>()` is the one-call setup: it registers `TPublisher` as a keyed `IAuditLogPublisher` (keyed by `typeof(TDbContext).FullName`) and internally calls `AddEfCoreAuditHook<TDbContext>()`, which registers the `AuditLogOptions` and adds `EfCoreAuditHook` via `services.AddHook<TDbContext, EfCoreAuditHook>()`. If you want the hook without a publisher yet (e.g. registering publishers separately, or several of them), call `AddEfCoreAuditHook<TDbContext>()` directly and add publishers with `services.AddKeyedScoped<IAuditLogPublisher, TPublisher>(typeof(TDbContext).FullName!)`.

Step 3 is independent of the other two: `AddCurrentUserProvider<TDbContext, TProvider>()` attaches the audit hook itself, so it works on its own when you only want `CreatedBy`/`UpdatedBy` filled and no audit trail published — see [`ICurrentUserProvider`](#icurrentuserprovider--who-the-change-is-attributed-to).

## 🧩 Features

### `AuditLogEntry` — the captured record shape

Every audited operation produces one `AuditLogEntry` (a `sealed record` implementing `IAuditedProperties` from Abstractions):

```csharp
public sealed record AuditLogEntry : IAuditedProperties
{
    public required AuditLogAction Action { get; init; }       // Created | Updated | Deleted
    public required DateTimeOffset CreatedOn { get; init; }
    public DateTimeOffset? UpdatedOn { get; init; }
    public required IDictionary<string, object?> Keys { get; init; }        // primary-key name -> value
    public required IReadOnlyList<AuditFieldChange> Changes { get; init; }  // field-level diffs
    public required string CreatedBy { get; init; }
    public required string EntityName { get; init; }            // entry.Entity.GetType().Name
    public string? UpdatedBy { get; init; }
}

public sealed record AuditFieldChange
{
    public required string FieldName { get; init; }
    public object? OldValue { get; init; }
    public object? NewValue { get; init; }
}
```

`Keys` comes from the entity's EF-mapped primary key (via the same `GetEntityKeyValues()` extension used elsewhere in DKNet), so composite keys are represented as multiple dictionary entries. `CreatedBy`/`CreatedOn`/`UpdatedBy`/`UpdatedOn` are copied from the audited entity itself (it must implement `IAuditedProperties`), not from the audit entry's own creation time — and when an `ICurrentUserProvider` is registered, the same hook has already stamped those properties onto the entity earlier in the *same* `BeforeSaveAsync` pass (see [`ICurrentUserProvider`](#icurrentuserprovider--who-the-change-is-attributed-to)), so the entry and the row it describes can never disagree.

### `EfCoreAuditHook` — how capture happens

`EfCoreAuditHook` is an `internal` class in `DKNet.EfCore.AuditLogs.Internals` that derives from `HookAsync`, so it implements the combined **`IHookAsync`** interface from `DKNet.EfCore.Hooks` (`IBeforeSaveHookAsync` + `IAfterSaveHookAsync`) — the same interface any other hook (domain events, data authorization, …) implements to join the same `SaveChanges` pipeline. You never construct or reference `EfCoreAuditHook` directly; `AddEfCoreAuditHook<TDbContext>` registers it for you.

Its mechanics, split across the two save phases:

- **`BeforeSaveAsync`** — first stamps `CreatedBy`/`UpdatedBy` from the registered `ICurrentUserProvider`, if there is one (`StampCurrentUser`, a no-op otherwise). Then, for every tracked entity whose original state is `Added`, `Modified`, or `Deleted`, calls an internal `entry.BuildAuditLog(...)` to produce an `AuditLogEntry` (entities that don't implement `IAuditedProperties`, or that are excluded per the configured behaviour — see [Configuration reference](#-configuration-reference) — yield `null` and are skipped). The resulting entries are cached in memory keyed by the `DbContext` instance's `ContextId`, so entries built here survive to the after-save phase of the *same* save call.
- **`AfterSaveAsync`** — after the save has completed successfully, retrieves the cached entries for this `DbContext` instance, removes them from the cache, and publishes them to every `IAuditLogPublisher` registered for that `DbContext` type. This is a normal `await` inside `AfterSaveAsync` — publishing latency is part of `SaveChangesAsync`'s own completion time, it is not fire-and-forget.

For `Created` entities, `Changes` is always empty — the field-diff loop only runs when the original state is not `Added` — so a create audit entry carries `Action = Created`, `Keys`, and the audit metadata, but no field-level detail. For `Deleted` entities, every captured property gets `NewValue = null` and `OldValue` set to the last known value (or the redaction sentinel).

### `ICurrentUserProvider` — who the change is attributed to

```csharp
namespace DKNet.EfCore.AuditLogs;

public interface ICurrentUserProvider
{
    string? GetCurrentUser();
}
```

Implement it over whatever your application already uses to represent the caller, and register it with
`AddCurrentUserProvider<TDbContext, TProvider>()`:

```csharp
public sealed class SignedInUserProvider(ICurrentPrincipal principal) : ICurrentUserProvider
{
    // Return a stable, non-personal identifier — see the privacy note at the end of this section.
    public string? GetCurrentUser() => principal.SubjectId; // e.g. "sub-8f21c0"
}

services.AddDbContextWithHook<AppDbContext>((provider, options) => options.UseSqlServer(connectionString));
services.AddCurrentUserProvider<AppDbContext, SignedInUserProvider>();
```

What that one call does, from `EfCoreAuditLogSetup`:

- Registers `TProvider` as a **scoped, application-wide** `ICurrentUserProvider` — guarded by
  `IsRegistered<ICurrentUserProvider>()`, so the **first caller wins**. The provider is *not* keyed per
  `DbContext`: a later call naming another `TDbContext` attaches the hook there too but keeps the provider already
  registered. This is the same single-active-provider shape as `AddDataOwnerProvider`.
- Attaches `EfCoreAuditHook` to `TDbContext` via `AddHook<TDbContext, EfCoreAuditHook>()`, so the call stands on
  its own when you want the stamping and nothing else — the hook stamps whether or not any `IAuditLogPublisher` is
  registered.
- Registers the default `AuditLogOptions` **only when none is registered yet**, so it never overwrites the
  `behaviour`/`propertyPolicy` an earlier `AddEfCoreAuditHook`/`AddEfCoreAuditLogs` call chose. The two calls may
  therefore appear in either order.

Stamping happens in `StampCurrentUser`, called at the top of `BeforeSaveAsync` **before** the audit entries are
built — which is what makes a published entry carry the same values the row was saved with:

| Original state | Stamped from the current user | Left alone when |
|---|---|---|
| `Added` | `CreatedBy`, `CreatedOn` (`DateTimeOffset.UtcNow`) | `CreatedBy` is already non-empty — first write wins, so a domain factory's `SetCreatedBy(...)` survives |
| `Modified` | `UpdatedBy`, `UpdatedOn` (`DateTimeOffset.UtcNow`) | a domain method already changed `UpdatedBy`/`UpdatedOn` in this change set (`SetUpdatedBy(...)`), or the entity has no mapped `UpdatedBy` property |
| `Deleted` | nothing | always — a delete is recorded in the trail, never stamped |

`GetCurrentUser()` returning `null` or empty makes the whole pass a no-op: the save still succeeds and this
package writes nothing.

#### Composing with `DKNet.EfCore.DataAuthorization`

Both providers are optional and each works without the other. `DataOwnerHook` takes the same
`ICurrentUserProvider` as an optional dependency and decides from its **value for that save**, never from hook
registration order — so the two hooks cannot fight over the audit fields:

| Registered | `CreatedBy`/`UpdatedBy` come from | `OwnedBy` comes from |
|---|---|---|
| current-user provider only | `GetCurrentUser()` | not stamped |
| ownership provider only | `IDataOwnerProvider.GetOwnershipKey()` | the same ownership key |
| both, `GetCurrentUser()` returned a value | `GetCurrentUser()` | the ownership key |
| both, `GetCurrentUser()` returned `null`/empty | the ownership key — the pre-existing behaviour | the ownership key |

The last row is why adding a current-user provider is a backwards-compatible change: a background job or an
unauthenticated request for which the provider has no user still lands the tenant key in `CreatedBy`/`UpdatedBy`,
exactly as before. With neither provider supplying a value, the audit properties are left as the entity set them.

#### Privacy: the value is published unmasked

Whatever `GetCurrentUser()` returns reaches every registered `IAuditLogPublisher` **in full** — the redaction
rules above cover entity property values, not the audit identity itself. An application subject to a personal-data
rule (GDPR, PDPA) should therefore return a stable, non-personal identifier such as the token subject id
(`"sub-8f21c0"`), rather than an email address or any other directly identifying value.

This does not apply to the error log written when a publisher throws (see
[Gotchas & limits](#-gotchas--limits)): that log names the publisher, the entity name(s), and the entry count —
it no longer serializes the failed batch's entry values, so a publisher failure cannot itself leak a redacted or
unredacted value into the application log.

### `IAuditLogPublisher` — where the entries go

```csharp
public interface IAuditLogPublisher
{
    Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default);
}
```

This is the extension point: implement it to ship a `SaveChangesAsync` call's audit batch to a database table, queue, log sink, or anywhere else. Publishers are registered as **keyed scoped services**, keyed by `typeof(TDbContext).FullName`, so different `DbContext` types can have entirely different publishers, and multiple publishers can be registered for the same `DbContext` (all are invoked; one publisher throwing does not stop the others — see [Gotchas & limits](#-gotchas--limits)).

```csharp
public sealed class ConsoleAuditLogPublisher : IAuditLogPublisher
{
    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
    {
        foreach (var log in logs)
        {
            var fields = string.Join(", ", log.Changes.Select(c => $"{c.FieldName}: {c.OldValue} -> {c.NewValue}"));
            Console.WriteLine($"[{log.Action}] {log.EntityName} by {log.UpdatedBy ?? log.CreatedBy} — {fields}");
        }
        return Task.CompletedTask;
    }
}
```

Registering more than one publisher for the same `DbContext`:

```csharp
services.AddEfCoreAuditHook<AppDbContext>(); // hook only, no publisher yet
services.AddKeyedScoped<IAuditLogPublisher, ConsoleAuditLogPublisher>(typeof(AppDbContext).FullName!);
services.AddKeyedScoped<IAuditLogPublisher, DatabaseAuditLogPublisher>(typeof(AppDbContext).FullName!);
```

To resolve the publishers registered for a `DbContext` type yourself (e.g. in a test, or for manual invocation), use the `GetAuditLogPublishers<TDbContext>()` extension on `IServiceProvider`:

```csharp
var publishers = serviceProvider.GetAuditLogPublishers<AppDbContext>();
```

### Sensitive-data redaction and its interaction with the Abstractions attributes

`SensitiveDataPatterns` (internal to this package) is a hardcoded deny-list of name fragments — `password`, `secret`, `token`, `apikey`, `api_key`, `ssn`, `socialsecuritynumber`, `creditcard`, `cvv`, `pin`, `connectionstring`, `privatekey`, `passphrase`, `accesskey`, `salt` — matched case-insensitively against the property name, plus any property of CLR type `System.Security.SecureString`. A match causes the field's `OldValue`/`NewValue` to be replaced with the sentinel string `"***REDACTED***"` in the captured `AuditFieldChange` — the field still appears in `Changes` (so you can see it changed) but never its value.

This interacts with three attributes defined in `DKNet.EfCore.Abstractions.Attributes`, plus `[Encrypted]` from `DKNet.EfCore.Encryption`:

- **`[IgnoreAuditLog]`** (class or property) — excludes the entity or property from audit capture entirely; an ignored property never appears in `Changes` at all, redacted or not. An entity type marked at class level produces no `AuditLogEntry` regardless of `AuditLogBehaviour`.
- **`[AuditLog]`** (class or property) — at class level, required for the entity to be audited under `AuditLogBehaviour.OnlyAttributedAuditedEntities` (see [Configuration reference](#-configuration-reference)). At property level, it forces plaintext capture of that property under `AuditPropertyPolicy.RedactSensitive` even if its name matches a sensitive pattern — but it does **not** override `[SensitiveData]` or `[Encrypted]` on the same property.
- **`[SensitiveData]`** (property only) — always redacts the property's value, unconditionally, even if the same property also carries `[AuditLog]`. Use it for values that don't match the built-in name patterns but must never appear in an audit trail (e.g. a `Notes` field that happens to hold PII).
- **`[Encrypted]`** (`DKNet.EfCore.Encryption.Attributes.EncryptedAttribute`, property only) — a property encrypted at rest via [DKNet.EfCore.Encryption](./DKNet.EfCore.Encryption.md) is redacted unconditionally, exactly like `[SensitiveData]`, winning over `[AuditLog]` on the same property. This package matches by attribute **type name**, not type identity — it takes no project reference on `DKNet.EfCore.Encryption` — so any attribute of your own named `EncryptedAttribute` also triggers redaction here.

```csharp
public sealed class Customer : AuditedEntity<Guid>
{
    [Encrypted] // encrypted at rest, and always redacted in the audit trail
    public string? TaxId { get; set; }
}
```

Redaction here is unchanged by role-gated API filtering. `[SensitiveData]` now accepts optional role names, but this package ignores them: a declared-sensitive value is redacted in every audit entry, for every reader, exactly as before. What the roles do — withhold the property from an API response for callers who don't hold one — is `DKNet.EfCore.Extensions`' opt-in and never touches audit capture; see [Withhold sensitive properties from unauthorised callers](./DKNet.EfCore.Extensions.md#withhold-sensitive-properties-from-unauthorised-callers). The reverse is also worth stating plainly: **the built-in name deny-list is an audit-log default only.** A property redacted because its name contains `token` or `apikey`, with no attribute on it, is still returned in full by the API — the response filter acts on an explicit `[SensitiveData]` declaration and nothing else. That gap is a deliberate, accepted decision rather than an oversight; close it for a given property by declaring it `[SensitiveData]`.

```csharp
public sealed class ApiClient : AuditedEntity<Guid>
{
    public required string Name { get; set; }

    [AuditLog] // name matches the "token" pattern, but this forces plaintext capture
    public DateTimeOffset TokenExpiryUtc { get; set; }

    [SensitiveData] // always redacted, regardless of name or [AuditLog]
    public string? InternalNotes { get; set; }

    [IgnoreAuditLog] // never appears in Changes at all
    public byte[]? Thumbnail { get; set; }
}
```

## ⚙️ Configuration reference

Both `AddEfCoreAuditHook<TDbContext>` and `AddEfCoreAuditLogs<TDbContext, TPublisher>` take the same two optional
parameters — there is no options class to configure post-registration:

| Option | Type | Default | Effect |
|---|---|---|---|
| `behaviour` | `AuditLogBehaviour` | `IncludeAllAuditedEntities` | `IncludeAllAuditedEntities` audits every `IAuditedProperties` entity not marked `[IgnoreAuditLog]`; `OnlyAttributedAuditedEntities` audits only entities marked `[AuditLog]` at class level. |
| `propertyPolicy` | `AuditPropertyPolicy` | `RedactSensitive` | `RedactSensitive` captures every non-ignored property, replacing sensitive-looking values with `"***REDACTED***"`; `OnlyAttributedProperties` captures only properties marked `[AuditLog]` and omits the rest. |

`AddCurrentUserProvider<TDbContext, TProvider>()` takes no arguments at all. It registers those same defaults only
when no `AuditLogOptions` is registered yet, so non-default values must come from an
`AddEfCoreAuditHook`/`AddEfCoreAuditLogs` call — in either order, since neither call overwrites the other's options.

The two values reach the hook through an internal `AuditLogOptions` singleton, so they are fixed at registration time
for the whole application — there is no per-save or per-entity override.

```csharp
public enum AuditLogBehaviour
{
    IncludeAllAuditedEntities,     // default: every IAuditedProperties entity is audited
    OnlyAttributedAuditedEntities  // only entities marked [AuditLog] at class level are audited
}

public enum AuditPropertyPolicy
{
    RedactSensitive,          // default: capture every non-ignored property, redact sensitive ones
    OnlyAttributedProperties  // capture only properties explicitly marked [AuditLog]; omit everything else
}

services.AddEfCoreAuditLogs<AppDbContext, MyAuditLogPublisher>(
    behaviour: AuditLogBehaviour.OnlyAttributedAuditedEntities,
    propertyPolicy: AuditPropertyPolicy.OnlyAttributedProperties);
```

Defaults (`AuditLogBehaviour.IncludeAllAuditedEntities` + `AuditPropertyPolicy.RedactSensitive`) favor completeness: every `IAuditedProperties` entity not explicitly opted out via `[IgnoreAuditLog]` gets audited, and sensitive-looking fields are redacted rather than omitted. Switch both to the `OnlyAttributed*` values for an explicit allow-list model where nothing is captured unless a developer opted it in with `[AuditLog]`.

## 🧱 Where it fits

Two gates decide what ends up in the trail: an entity-level gate driven by `AuditLogBehaviour`, and a property-level
gate driven by `AuditPropertyPolicy` and the redactor:

![Data-flow diagram of audit capture: snapshot entries pass an entity gate that requires IAuditedProperties and honours the configured behaviour, then a property gate that applies AuditPropertyPolicy and routes sensitive values through the redactor, producing an AuditLogEntry that is published after the write to every IAuditLogPublisher keyed to the DbContext.](../diagrams/efcore-auditlogs-capture.svg)

- **[DKNet.EfCore.Abstractions](./DKNet.EfCore.Abstractions.md)** supplies the contract: entities must implement `IAuditedProperties` (directly, or via the `AuditedEntity`/`AuditedEntity<TKey>` base classes) to be eligible for auditing at all, and the `[AuditLog]`/`[IgnoreAuditLog]`/`[SensitiveData]` attributes that steer what gets captured live there, not in this package.
- **[DKNet.EfCore.Hooks](./DKNet.EfCore.Hooks.md)** provides the `SaveChanges` pipeline. `EfCoreAuditHook` is a normal `IHookAsync` hook, registered with the same `AddHook<TDbContext, THook>()` call any other hook uses, and only runs if the `DbContext` was registered via `AddDbContextWithHook<TDbContext>` (or `options.UseHooks<TDbContext>(provider)` manually) — auditing shares the exact same wiring requirement as domain events or data authorization hooks on the same `DbContext`.
- Because multiple hooks can be registered for one `DbContext`, the audit hook runs alongside e.g. a domain-events hook or a data-authorization hook in the same before/after-save pass; `HookRunnerInterceptor` runs all `BeforeSaveAsync` hooks in DI registration order, then (after the underlying `SaveChangesAsync` succeeds) all `AfterSaveAsync` hooks in DI registration order — there is no explicit priority system, so if audit ordering relative to another hook matters, control it via registration order.

## ⚠️ Gotchas & limits

- **Registering the hook is not enough.** `AddEfCoreAuditHook`/`AddEfCoreAuditLogs` only add DI registrations; without `AddDbContextWithHook<TDbContext>` (or a manual `options.UseHooks<TDbContext>(provider)`), the `HookRunnerInterceptor` is never attached to the `DbContext` and the audit hook silently never runs.
- **Only `IAuditedProperties` entities are audited.** A plain entity that doesn't implement it (or inherit `AuditedEntity`/`AuditedEntity<TKey>`) is always skipped, regardless of `AuditLogBehaviour`.
- **Redaction hides values, not the fact of a change.** A redacted field still appears in `Changes` with both `OldValue` and `NewValue` set to `"***REDACTED***"`, so the entry tells you a sensitive field changed but never what it changed to — and it can no longer tell you whether the new value actually differs from the old one.
- **Creates carry no field diff.** `Action == AuditLogAction.Created` entries always have an empty `Changes` list by design — if you need a full snapshot of a newly created entity, a publisher must fetch it separately using `Keys`.
- **Publishing is awaited, not fire-and-forget.** `AfterSaveAsync` awaits every registered publisher in turn, so a slow or blocking `IAuditLogPublisher` adds directly to `SaveChangesAsync` latency for every save that produced audit entries. Keep publishers fast, or hand off to a background queue from inside your publisher.
- **Publisher exceptions are swallowed, not surfaced.** A throwing `PublishAsync` is caught, optionally logged (if an `ILogger<EfCoreAuditHook>` is configured, at `Error` level — naming the publisher, the entity name(s), and the entry count, never the entries' values), and does not fail the save — other registered publishers still run. This is an accepted trade-off, not a bug: `PublishLogsAsync` runs from `AfterSaveAsync`, after the write has already committed, so there is no recovery path for a dropped audit entry — a failure here cannot roll back the save, and by the time it happens the one chance to persist the audit entry atomically with the write is already gone. Retrying or queuing *inside* `IAuditLogPublisher` doesn't close that gap, since the entry it would retry was never durably recorded in the first place. If you need at-least-once delivery of audit logs, don't rely on this hook for it — write the entries to an outbox table from a `BeforeSaveHookAsync` inside the *same* save transaction as the entity write, and drain that table with a separate dispatcher.
- **Performance cost scales with tracked entities and properties per `SaveChangesAsync` call.** `BeforeSaveAsync` walks every tracked `Added`/`Modified`/`Deleted` entry and every one of its mapped scalar properties (with a reflection-based attribute check per property) on every save. Narrowing scope with `AuditLogBehaviour.OnlyAttributedAuditedEntities` and/or `AuditPropertyPolicy.OnlyAttributedProperties` reduces that cost for hot paths.
- **Navigation properties and collections are not diffed.** Only the scalar/mapped properties on `entry.Properties` are captured; related-entity changes are audited independently, on their own `AuditLogEntry`, if the related entity itself implements `IAuditedProperties`.
- **`ICurrentUserProvider` is application-wide, and the first registration wins.** `AddCurrentUserProvider<TDbContext, TProvider>()` registers the provider un-keyed behind an `IsRegistered<ICurrentUserProvider>()` guard, so a second call with a *different* `TProvider` silently keeps the first one — only the hook attachment to the new `TDbContext` takes effect. There is no per-`DbContext` current-user provider.
- **The current-user value is published unmasked.** It is the audit identity, not an entity property, so no redaction rule applies to it. Return a stable non-personal identifier (a token subject id) if the audit trail is subject to a personal-data rule.
- **No current user means no stamp from this package, not an error.** `GetCurrentUser()` returning `null`/empty skips stamping silently; if `DKNet.EfCore.DataAuthorization` is also registered, its ownership key fills `CreatedBy`/`UpdatedBy` for that save instead. A registration mistake therefore shows up as a tenant key in `CreatedBy`, or as blank audit fields — never as an exception.
- **Registering the same publisher type twice is a no-op.** `AddEfCoreAuditLogs<TDbContext, TPublisher>` returns early if a keyed registration for that exact `TPublisher` and `DbContext` key already exists, so a second call (e.g. from two library extension methods) does not double-publish — but it also silently ignores any different `behaviour`/`propertyPolicy` you passed on the second call.

## 🔗 Related packages

- [DKNet.EfCore.Hooks](./DKNet.EfCore.Hooks.md) – the `SaveChanges` pipeline this package registers into. Reach for it
  directly when you need a custom before/after-save hook of your own.
- [DKNet.EfCore.Abstractions](./DKNet.EfCore.Abstractions.md) – defines `IAuditedProperties`, `AuditedEntity<TKey>`,
  and the `[AuditLog]`/`[IgnoreAuditLog]`/`[SensitiveData]` attributes. Reach for it to make an entity auditable in
  the first place.
- [DKNet.EfCore.Events](./DKNet.EfCore.Events.md) – dispatches domain events through the same pipeline. Reach for it
  when other parts of the system must *react* to a change rather than record it.
- [DKNet.EfCore.Encryption](./DKNet.EfCore.Encryption.md) – column-level encryption. Reach for it to protect a value
  at rest; this package only decides whether the value appears in an audit entry.
- [DKNet.EfCore.DataAuthorization](./DKNet.EfCore.DataAuthorization.md) – row-level ownership filtering on the same
  hook pipeline. Reach for it to control who can *see* a row, not who changed it. It also supplies the ownership-key
  fallback for `CreatedBy`/`UpdatedBy` when no current user is available for a save.
