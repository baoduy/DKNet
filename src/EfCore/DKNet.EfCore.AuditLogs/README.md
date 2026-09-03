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

## Quick start

```csharp
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.Hooks;

// 1. Register the DbContext through the hook-aware overload.
services.AddDbContextWithHook<AppDbContext>((provider, options) =>
    options.UseSqlServer(connectionString));

// 2. Register the audit hook plus a publisher, keyed to AppDbContext.
services.AddEfCoreAuditLogs<AppDbContext, MyAuditLogPublisher>();

// 3. Implement the publisher.
public sealed class MyAuditLogPublisher : IAuditLogPublisher
{
    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
    {
        foreach (var log in logs)
            Console.WriteLine($"[{log.Action}] {log.EntityName} by {log.UpdatedBy ?? log.CreatedBy}");
        return Task.CompletedTask;
    }
}
```

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

Full documentation: https://github.com/baoduy/DKNet/blob/main/docs/EfCore/DKNet.EfCore.AuditLogs.md
