# DKNet.EfCore.AuditLogs

A `SaveChanges` interceptor that captures a structured, field-level change record for every created, updated, or
deleted entity, and hands the batch to publishers you register.

## ✨ Why use it?

- **No hand-rolled `ChangeTracker` walk** — the hook enumerates every `Added`/`Modified`/`Deleted` entry and diffs its
  mapped scalar properties for you, instead of that loop being copy-pasted into each `DbContext`.
- **Sensitive values are redacted by default** — property names matching a built-in deny-list (`password`, `token`,
  `apikey`, `ssn`, `creditcard`, …) and any `SecureString` property are captured as `"***REDACTED***"`, so an audit
  trail cannot silently become a credential leak.
- **Declarative opt-in and opt-out** — `[AuditLog]`, `[IgnoreAuditLog]`, and `[SensitiveData]` on the entity decide
  what is captured, so the policy lives next to the model rather than in audit plumbing.
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
```

`AddEfCoreAuditLogs<TDbContext, TPublisher>()` is the one-call setup: it registers `TPublisher` as a keyed `IAuditLogPublisher` (keyed by `typeof(TDbContext).FullName`) and internally calls `AddEfCoreAuditHook<TDbContext>()`, which registers the `AuditLogOptions` and adds `EfCoreAuditHook` via `services.AddHook<TDbContext, EfCoreAuditHook>()`. If you want the hook without a publisher yet (e.g. registering publishers separately, or several of them), call `AddEfCoreAuditHook<TDbContext>()` directly and add publishers with `services.AddKeyedScoped<IAuditLogPublisher, TPublisher>(typeof(TDbContext).FullName!)`.

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

`Keys` comes from the entity's EF-mapped primary key (via the same `GetEntityKeyValues()` extension used elsewhere in DKNet), so composite keys are represented as multiple dictionary entries. `CreatedBy`/`CreatedOn`/`UpdatedBy`/`UpdatedOn` are copied from the audited entity itself (it must implement `IAuditedProperties`), not from the audit entry's own creation time.

### `EfCoreAuditHook` — how capture happens

`EfCoreAuditHook` is an `internal` class in `DKNet.EfCore.AuditLogs.Internals` that derives from `HookAsync`, so it implements the combined **`IHookAsync`** interface from `DKNet.EfCore.Hooks` (`IBeforeSaveHookAsync` + `IAfterSaveHookAsync`) — the same interface any other hook (domain events, data authorization, …) implements to join the same `SaveChanges` pipeline. You never construct or reference `EfCoreAuditHook` directly; `AddEfCoreAuditHook<TDbContext>` registers it for you.

Its mechanics, split across the two save phases:

- **`BeforeSaveAsync`** — for every tracked entity whose original state is `Added`, `Modified`, or `Deleted`, calls an internal `entry.BuildAuditLog(...)` to produce an `AuditLogEntry` (entities that don't implement `IAuditedProperties`, or that are excluded per the configured behaviour — see [Configuration reference](#-configuration-reference) — yield `null` and are skipped). The resulting entries are cached in memory keyed by the `DbContext` instance's `ContextId`, so entries built here survive to the after-save phase of the *same* save call.
- **`AfterSaveAsync`** — after the save has completed successfully, retrieves the cached entries for this `DbContext` instance, removes them from the cache, and publishes them to every `IAuditLogPublisher` registered for that `DbContext` type. This is a normal `await` inside `AfterSaveAsync` — publishing latency is part of `SaveChangesAsync`'s own completion time, it is not fire-and-forget.

For `Created` entities, `Changes` is always empty — the field-diff loop only runs when the original state is not `Added` — so a create audit entry carries `Action = Created`, `Keys`, and the audit metadata, but no field-level detail. For `Deleted` entities, every captured property gets `NewValue = null` and `OldValue` set to the last known value (or the redaction sentinel).

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

This interacts with three attributes defined in `DKNet.EfCore.Abstractions.Attributes`:

- **`[IgnoreAuditLog]`** (class or property) — excludes the entity or property from audit capture entirely; an ignored property never appears in `Changes` at all, redacted or not. An entity type marked at class level produces no `AuditLogEntry` regardless of `AuditLogBehaviour`.
- **`[AuditLog]`** (class or property) — at class level, required for the entity to be audited under `AuditLogBehaviour.OnlyAttributedAuditedEntities` (see [Configuration reference](#-configuration-reference)). At property level, it forces plaintext capture of that property under `AuditPropertyPolicy.RedactSensitive` even if its name matches a sensitive pattern — but it does **not** override `[SensitiveData]` on the same property.
- **`[SensitiveData]`** (property only) — always redacts the property's value, unconditionally, even if the same property also carries `[AuditLog]`. Use it for values that don't match the built-in name patterns but must never appear in an audit trail (e.g. a `Notes` field that happens to hold PII).

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
- **Publisher exceptions are swallowed, not surfaced.** A throwing `PublishAsync` is caught, optionally logged (if an `ILogger<EfCoreAuditHook>` is configured, at `Error` level, best-effort including a JSON dump of the failed batch), and does not fail the save — other registered publishers still run. If you need guaranteed delivery, build that guarantee (retry, durable queue) inside your own publisher.
- **Performance cost scales with tracked entities and properties per `SaveChangesAsync` call.** `BeforeSaveAsync` walks every tracked `Added`/`Modified`/`Deleted` entry and every one of its mapped scalar properties (with a reflection-based attribute check per property) on every save. Narrowing scope with `AuditLogBehaviour.OnlyAttributedAuditedEntities` and/or `AuditPropertyPolicy.OnlyAttributedProperties` reduces that cost for hot paths.
- **Navigation properties and collections are not diffed.** Only the scalar/mapped properties on `entry.Properties` are captured; related-entity changes are audited independently, on their own `AuditLogEntry`, if the related entity itself implements `IAuditedProperties`.
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
  hook pipeline. Reach for it to control who can *see* a row, not who changed it.
