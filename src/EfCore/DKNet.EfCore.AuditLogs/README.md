# DKNet.EfCore.AuditLogs

A `DKNet.EfCore.Hooks`-based `SaveChanges` interceptor that captures a structured, field-level audit trail of entity changes — with automatic redaction of likely-sensitive values — and hands finished batches to your own publisher(s).

## Install

```bash
dotnet add package DKNet.EfCore.AuditLogs
```

## Features

- Automatic before/after-`SaveChanges` capture of Created/Updated/Deleted entities implementing `IAuditedProperties`, with per-field old/new value diffs.
- Built-in redaction of likely-sensitive properties (passwords, tokens, connection strings, `SecureString`, …), overridable per-property via `[AuditLog]` and forced via `[SensitiveData]` from `DKNet.EfCore.Abstractions`.
- Pluggable `IAuditLogPublisher` extension point — ship audit batches to a database, queue, log sink, or anywhere else; multiple publishers per `DbContext` are supported.
- Configurable scope: audit every entity or only those explicitly marked `[AuditLog]`, and capture every property or only allow-listed ones.
- Optional signed-in-user stamping: register an `ICurrentUserProvider` and the hook fills `CreatedBy`/`UpdatedBy` from the application's current user, independently of the tenant ownership key supplied by `DKNet.EfCore.DataAuthorization`.

## Quick start

```csharp
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.Hooks;

// 1. Register the DbContext through the hook-aware overload.
services.AddDbContextWithHook<AppDbContext>((provider, options) =>
    options.UseSqlServer(connectionString));

// 2. Register the audit hook plus a publisher, keyed to AppDbContext.
services.AddEfCoreAuditLogs<AppDbContext, MyAuditLogPublisher>();

// 3. Optional: fill CreatedBy/UpdatedBy from the signed-in user.
services.AddCurrentUserProvider<AppDbContext, SignedInUserProvider>();

// 4. Implement the publisher.
public sealed class MyAuditLogPublisher : IAuditLogPublisher
{
    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
    {
        foreach (var log in logs)
            Console.WriteLine($"[{log.Action}] {log.EntityName} by {log.UpdatedBy ?? log.CreatedBy}");
        return Task.CompletedTask;
    }
}

// 5. Implement the current-user provider, resolving whatever your application already uses to
//    represent the caller. Return a stable, non-personal identifier — see "Current user" below.
public sealed class SignedInUserProvider(ICurrentPrincipal principal) : ICurrentUserProvider
{
    public string? GetCurrentUser() => principal.SubjectId; // e.g. "sub-8f21c0"
}
```

`AddCurrentUserProvider<TDbContext, TProvider>()` attaches the audit hook itself, so step 3 works on its own
when you only want the stamping and no publisher. It never overwrites the `behaviour`/`propertyPolicy` a
previous `AddEfCoreAuditHook`/`AddEfCoreAuditLogs` call registered, so the two calls can appear in either
order.

## Customisation reference

`AddEfCoreAuditHook<TDbContext>` and `AddEfCoreAuditLogs<TDbContext, TPublisher>` take the same two optional
arguments. They are fixed at registration time for the whole application — there is no per-save or per-entity
override, and no options class to reconfigure afterwards.

| Option | Type | Default | Effect |
|---|---|---|---|
| `behaviour` | `AuditLogBehaviour` | `IncludeAllAuditedEntities` | `IncludeAllAuditedEntities` audits every `IAuditedProperties` entity not marked `[IgnoreAuditLog]`. `OnlyAttributedAuditedEntities` audits only entities marked `[AuditLog]` at class level. |
| `propertyPolicy` | `AuditPropertyPolicy` | `RedactSensitive` | `RedactSensitive` captures every non-ignored property, replacing sensitive-looking values with `***REDACTED***`. `OnlyAttributedProperties` captures only properties marked `[AuditLog]`. |

Attribute-level control comes from `DKNet.EfCore.Abstractions`:

| Attribute | On | Effect |
|---|---|---|
| `[IgnoreAuditLog]` | class or property | Excluded unconditionally, whatever the behaviour and policy. |
| `[AuditLog]` | class | Opts the entity in under `OnlyAttributedAuditedEntities`. |
| `[AuditLog]` | property | Forces plaintext past the sensitive-name patterns, and allow-lists it under `OnlyAttributedProperties`. |
| `[SensitiveData]` | property | Always redacted, even alongside `[AuditLog]`. |

The built-in sensitive-name fragments are `password`, `secret`, `token`, `apikey`, `api_key`, `ssn`,
`socialsecuritynumber`, `creditcard`, `cvv`, `pin`, `connectionstring`, `privatekey`, `passphrase`, `accesskey`
and `salt` (case-insensitive substring match), plus any property typed `SecureString`. The list is not
configurable — use `[SensitiveData]` to add to it and `[AuditLog]` to opt out of it per property.

An entity that does not implement `IAuditedProperties` is skipped before any attribute is inspected.

### Current user (`CreatedBy` / `UpdatedBy`)

The current-user provider is optional. Register one with
`AddCurrentUserProvider<TDbContext, TProvider>()` and the hook stamps the audit identity from
`ICurrentUserProvider.GetCurrentUser()`:

- **`CreatedBy`/`CreatedOn` are written once, on insert.** A later update never rewrites them, so the
  original creator survives every subsequent save.
- **`UpdatedBy` follows the caller of each save** — except when a domain method already recorded a modifier
  for that change set with `SetUpdatedBy(...)`. An explicit modifier always wins over both the current user
  and the ownership key.
- **`GetCurrentUser()` returning `null` or empty stamps nothing** — the save still succeeds and the audit
  properties are left as they are.
- **Without a current-user provider nothing changes.** `CreatedBy`/`UpdatedBy` keep coming from the
  `IDataOwnerProvider` ownership key when `DKNet.EfCore.DataAuthorization` is in use, and stay unset when it
  is not.

Whatever the provider returns is published **unmasked** to every registered `IAuditLogPublisher` — the
redaction rules above cover entity property values, not the audit identity itself. An application subject to a
personal-data rule (GDPR, PDPA) should therefore return a stable, non-personal identifier such as the token
subject id (`"sub-8f21c0"`), not an email address or any other directly identifying value.

Ownership (`OwnedBy`) is a separate concern owned by
[`DKNet.EfCore.DataAuthorization`](../DKNet.EfCore.DataAuthorization/README.md); registering both providers is
supported and each fills only its own properties.

Full documentation: https://github.com/baoduy/DKNet/blob/main/docs/EfCore/DKNet.EfCore.AuditLogs.md
