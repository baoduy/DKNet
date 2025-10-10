# DKNet.EfCore.AuditLogs

Structured, fire-and-forget audit logging for Entity Framework Core using a lightweight hook/interceptor pipeline.

## Why

Most EF Core audit samples depend on change trackers at save time but return raw debug views or force synchronous
post-processing. This library:

- Captures only Modified / Deleted audited entities (skips pure Creates by design).
- Emits a structured audit record per entity including field-level diffs.
- Publishes asynchronously (fire-and-forget) so SaveChanges latency impact is minimal.
- Allows multiple pluggable publishers (database, queue, file, telemetry, etc.).

## Core Concepts

### Audited Entities

Implement `IAuditedProperties` directly or inherit from `AuditedEntity` / `AuditedEntity<TKey>` which provides:

- `CreatedBy`, `CreatedOn` (set once via `SetCreatedBy` – idempotent)
- `UpdatedBy`, `UpdatedOn` (set via `SetUpdatedBy` on each change)
- Convenience: `LastModifiedBy`, `LastModifiedOn`

Only entities implementing `IAuditedProperties` and whose original state (snapshot) is `Modified` or `Deleted` produce
audit logs.

### Structured Audit Record

`EfCoreAuditLog` contains:

- `CreatedBy`, `CreatedOn`, `UpdatedBy`, `UpdatedOn`
- `EntityName` – CLR type name
- `Changes` – `IReadOnlyList<EfCoreAuditFieldChange>`
    - Each item: `FieldName`, `OldValue`, `NewValue`
    - For Deletions: `NewValue` is always `null` (entity removed)
- `ChangedView` (obsolete legacy long EF debug view retained for backward compatibility – prefer `Changes`)

### Fire-and-Forget Publishing

After a successful `SaveChanges` the hook:

1. Builds audit logs in `BeforeSaveAsync` snapshot.
2. Queues publishing tasks (not awaited) in `AfterSaveAsync`.
3. Swallows publisher exceptions (optionally logs via injected `ILogger<EfCoreAuditHook>`). Your application code will
   not fail because a publisher failed.

### Publishers

Implement:

```csharp
public interface IAuditLogPublisher
{
    Task PublishAsync(IEnumerable<EfCoreAuditLog> logs, CancellationToken cancellationToken = default);
}
```

Register one or many. They are resolved every save; make them lightweight and thread-safe. Recommended: Singleton
services storing or forwarding logs in a concurrent collection or an async queue.

## Installation

NuGet (adjust version as needed):

```bash
dotnet add package DKNet.EfCore.AuditLogs
```

This library also requires:

```bash
dotnet add package Microsoft.EntityFrameworkCore
```

(Your DbContext provider – e.g. SqlServer / Sqlite / Npgsql – must also be installed.)

## Quick Start

```csharp
// 1. Define an audited entity
public sealed class Order : AuditedEntity<Guid>
{
    public required string Number { get; set; }
    public decimal Total { get; set; }
}

// 2. Custom publisher
public sealed class ConsoleAuditPublisher : IAuditLogPublisher
{
    public Task PublishAsync(IEnumerable<EfCoreAuditLog> logs, CancellationToken ct = default)
    {
        foreach (var log in logs)
        {
            Console.WriteLine($"[AUDIT] {log.EntityName} by {log.LastActor()} changes: " +
                              string.Join(", ", log.Changes.Select(c => $"{c.FieldName}:{c.OldValue}->{c.NewValue}")));
        }
        return Task.CompletedTask;
    }
}

// 3. Registration (Program.cs / Startup)
services
    .AddLogging() // optional for error logging
    .AddEfCoreAuditLogs<MyDbContext, ConsoleAuditPublisher>() // registers hook + publisher
    .AddDbContextWithHook<MyDbContext>((sp, opt) =>
    {
        opt.UseSqlite("Data Source=app.db");
        opt.EnableSensitiveDataLogging(); // helps diff; toggle for prod
    });

// 4. Use your entity normally
var order = new Order { Number = "INV-100", Total = 125m };
order.SetCreatedBy(userName);
ctx.Add(order); // Create - no audit log produced
await ctx.SaveChangesAsync();

order.Total = 150m;
order.SetUpdatedBy(userName); // or a domain helper method
await ctx.SaveChangesAsync(); // Audit log emitted
```

Helper extension for last actor (illustrative):

```csharp
public static class AuditLogExtensions
{
    public static string LastActor(this EfCoreAuditLog log) => log.UpdatedBy ?? log.CreatedBy;
}
```

## Registration Variants

| Scenario                                     | Method                                                                                                            |
|----------------------------------------------|-------------------------------------------------------------------------------------------------------------------|
| Only hook (publishers registered separately) | `services.AddEfCoreAuditLogs<TDbContext>()`                                                                       |
| Hook + single publisher                      | `services.AddEfCoreAuditLogs<TDbContext, TPublisher>()`                                                           |
| Multiple publishers                          | Call `AddEfCoreAuditLogs<TDbContext>()` then individual `services.AddSingleton<IAuditLogPublisher, XPublisher>()` |

## Entity Requirements & Lifecycle

- Creation only: skipped (no diff vs original).
- Modification: Only changed scalar properties included (`IsModified` or value inequality).
- Deletion: All scalar properties emitted with `NewValue = null`.
- Updating `CreatedBy` after initial set is ignored (idempotent). Ensure you call `SetCreatedBy` once.

## Error Handling

- Publisher exceptions are caught and logged (if an `ILogger<EfCoreAuditHook>` is registered). Processing continues
  silently.
- Logging failures are also swallowed.

## Performance Considerations

- Work done in `BeforeSaveAsync` captures snapshots before EF changes states.
- Publishing offloaded to thread pool; heavy publishers should queue to background workers.
- Avoid reflection or serialization on very large entities inside publishers synchronously.

## Concurrency & Thread Safety

- Because publishing is fire-and-forget, publishers must be thread-safe.
- Prefer using concurrent collections or channels (e.g. `System.Threading.Channels`) for buffering.
- For strict delivery guarantees or retries, build a durable queue publisher and disable swallowing internally.

## Extending

### Filter Properties

Wrap or fork `BuildAuditLog` (internal extension) to exclude sensitive fields (like passwords, secrets). Provide a
custom hook variant if needed.

### Enrich Audit Logs

Add correlation IDs, tenant IDs, etc. via a decorator publisher or by extending your entity base with additional
properties.

## Testing Guidance

- Register publishers as singletons for deterministic capture in tests.
- Use polling helpers (e.g. wait until `publisher.Received.Count >= expected`) because publishing is asynchronous.
- For integration tests needing deterministic timing, you can adapt the hook to a synchronous mode behind a test flag.

## Obsolete Members

- `ChangedView` is marked `[Obsolete]` and will be removed in a future major version. Migrate to the structured
  `Changes` collection.

## Limitations / Design Choices

- No out-of-the-box persistence; you decide how and where to store logs.
- Skips Added entities intentionally to reduce noise. If you need creation snapshots, extend the hook.
- Only scalar property diffs are emitted; navigation collections are ignored (design simplification).

## Sample Advanced Publisher (Serilog)

```csharp
public class SerilogAuditPublisher : IAuditLogPublisher
{
    public Task PublishAsync(IEnumerable<EfCoreAuditLog> logs, CancellationToken ct = default)
    {
        foreach (var log in logs)
        {
            Log.ForContext("entity", log.EntityName)
               .ForContext("createdBy", log.CreatedBy)
               .ForContext("updatedBy", log.UpdatedBy)
               .ForContext("changes", log.Changes.Select(c => new { c.FieldName, c.OldValue, c.NewValue }))
               .Information("EF audit");
        }
        return Task.CompletedTask;
    }
}
```

## Uninstall / Remove

Remove the hook registration or publisher registrations:

```csharp
// Remove: services.AddEfCoreAuditLogs<MyDbContext>();
// Replace with normal AddDbContext if audit no longer needed.
```

## Versioning & Compatibility

- Targets .NET 9 (adjust for your solution's TFM).
- Depends on EF Core runtime packages.

## Contributing

1. Fork & create feature branch
2. Add or update tests (poll for async events)
3. Submit PR describing changes & performance impact

## License

Choose a suitable OSS license (e.g. MIT) – add LICENSE file at root.

## Support / Questions

Open an issue in the repository or integrate with your organization’s internal support channel.

---
**TL;DR**: Register the hook, mark entities as audited, implement publishers, get structured async audit logs with
minimal impact on save performance.

