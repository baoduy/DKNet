# DKNet.EfCore.AuditLogs — AI Skill File

> **Package**: `DKNet.EfCore.AuditLogs`  
> **Minimum Version**: `10.0.0`  
> **Minimum .NET**: `net10.0`  
> **Source**: `src/EfCore/DKNet.EfCore.AuditLogs/`

---

## Purpose

Provides fire-and-forget, field-level audit logging for EF Core entities: automatically captures who changed which field from what value to what value on every `SaveChanges`, and routes the structured records to one or more pluggable publishers.

---

## When To Use

- ✅ You need to know **who** changed **which field** of an entity **from** what value **to** what value
- ✅ Regulatory / compliance requirements (GDPR, SOX, HIPAA) for data changes
- ✅ "Last modified by" and "last modified on" stamps on entities

## When NOT To Use

- ❌ You only need to know *when* something was saved — use `DKNet.EfCore.Hooks` to set a timestamp instead
- ❌ You need to react to a save and dispatch an external event — use `DKNet.EfCore.Events` for that
- ❌ You need to audit reads/queries — this library only tracks write operations

---

## Installation

```bash
dotnet add package DKNet.EfCore.AuditLogs
```

---

## Setup / DI Registration

```csharp
// 1. Make your entity implement IAuditedProperties (or inherit AuditedEntity<TKey>)
public class Product : AuditedEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// 2. Implement IAuditLogPublisher for your persistence target
public class DbAuditLogPublisher(AppDbContext db) : IAuditLogPublisher
{
    public async Task PublishAsync(
        IEnumerable<EfCoreAuditLog> logs, CancellationToken ct = default)
    {
        db.AuditLogs.AddRange(logs.Select(AuditLogEntity.From));
        await db.SaveChangesAsync(ct);
    }
}

// 3. Register in DI
services.AddEfCoreAuditLogs<AppDbContext, DbAuditLogPublisher>();
```

---

## Key API Surface

| Type / Method | Role |
|---|---|
| `AuditedEntity<TKey>` | Base class — provides `CreatedBy`, `CreatedOn`, `UpdatedBy`, `UpdatedOn` |
| `IAuditedProperties` | Interface — implement directly if you cannot use the base class |
| `IAuditLogPublisher` | Implement to persist/forward audit records |
| `EfCoreAuditLog` | Structured audit record: `EntityName`, `Changes` (list of `EfCoreAuditFieldChange`) |
| `EfCoreAuditFieldChange` | `FieldName`, `OldValue`, `NewValue` |
| `services.AddEfCoreAuditLogs<TDbContext, TPublisher>()` | Register the hook + publisher |

---

## Usage Pattern

```csharp
// Entity
public class Order : AuditedEntity<Guid>
{
    public string Status { get; set; } = "Pending";
    public decimal Total { get; set; }
}

// Publisher — saves to a separate audit table
public class AuditTablePublisher(AuditDbContext auditDb) : IAuditLogPublisher
{
    public async Task PublishAsync(IEnumerable<EfCoreAuditLog> logs, CancellationToken ct = default)
    {
        foreach (var log in logs)
        {
            auditDb.AuditEntries.Add(new AuditEntry
            {
                EntityName  = log.EntityName,
                ChangedBy   = log.UpdatedBy,
                ChangedOn   = log.UpdatedOn,
                FieldChanges = JsonSerializer.Serialize(log.Changes)
            });
        }
        await auditDb.SaveChangesAsync(ct);
    }
}

// Program.cs
services.AddEfCoreAuditLogs<AppDbContext, AuditTablePublisher>();
```

---

## Anti-Patterns

```csharp
// ❌ WRONG — overriding SaveChanges manually for audit (fragile, hard to test)
public override int SaveChanges()
{
    foreach (var entry in ChangeTracker.Entries())
        if (entry.State == EntityState.Modified)
            entry.Property("UpdatedOn").CurrentValue = DateTime.UtcNow;
    return base.SaveChanges();
}

// ✅ CORRECT — inherit AuditedEntity<T> and let the library handle it
public class Product : AuditedEntity<Guid> { ... }
services.AddEfCoreAuditLogs<AppDbContext, MyPublisher>();

// ❌ WRONG — using a long-running publisher (SaveChanges latency impact)
public async Task PublishAsync(IEnumerable<EfCoreAuditLog> logs, CancellationToken ct)
{
    await _externalApiClient.PostAsync("/audit", logs, ct);  // ← synchronous network call on hot path
}

// ✅ CORRECT — enqueue immediately, process out-of-band
public Task PublishAsync(IEnumerable<EfCoreAuditLog> logs, CancellationToken ct)
{
    foreach (var log in logs) _queue.Enqueue(log);
    return Task.CompletedTask;
}
```

---

## Composes With

| Library | How |
|---|---|
| `DKNet.EfCore.Hooks` | AuditLogs is built on top of the Hooks pipeline; do not add a competing hook that also reads `ChangeTracker` |
| `DKNet.EfCore.Extensions` | `AuditedEntity<T>` relies on entity configuration from Extensions for created/updated tracking |

---

## Security Notes

- Audit records may contain **PII** (field values before/after). Apply column-level encryption or masking before persisting if required.
- The `PublishAsync` method receives raw field values. Never log these to console or structured logs without scrubbing sensitive fields first.

---

## Test Example

```csharp
// Uses TestContainers.MsSql-backed integration test environment.
[Fact]
public async Task UpdateEntity_PublishesAuditLogWithFieldChange()
{
    // Arrange
    await using var db = CreateDbContext(_container.GetConnectionString());
    var published = new List<EfCoreAuditLog>();
    var publisher = new InMemoryAuditPublisher(published);
    services.AddEfCoreAuditLogs<TestDbContext, InMemoryAuditPublisher>();

    var order = new Order { Status = "Pending", Total = 100m };
    db.Orders.Add(order);
    await db.SaveChangesAsync();

    // Act
    order.Status = "Shipped";
    await db.SaveChangesAsync();
    await Task.Delay(50); // fire-and-forget — small wait for publisher

    // Assert
    published.ShouldNotBeEmpty();
    var change = published[0].Changes.ShouldHaveSingleItem();
    change.FieldName.ShouldBe("Status");
    change.OldValue.ShouldBe("Pending");
    change.NewValue.ShouldBe("Shipped");
}
```

---

## Quick Decision Guide

- Use this package when you need field-level change history.
- Use `EfCore.Hooks` alone for lightweight lifecycle callbacks without structured audit records.
- Use asynchronous/queued publishers to avoid save-path latency spikes.

---

## Version

| Version | Notes |
|---|---|
| `10.0.0` | Initial documentation — `AuditedEntity<T>`, `IAuditLogPublisher`, `EfCoreAuditLog` |
