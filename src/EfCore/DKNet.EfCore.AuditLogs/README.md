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

Full documentation: https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.AuditLogs.md
