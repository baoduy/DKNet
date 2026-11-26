# Capture entity changes

Part of [DKNet.EfCore.AuditLogs](index.md). Use the [Quick Start](index.md#-quick-start) context and publisher. After its first save, update the tracked item:

```csharp
var item = await db.Items.SingleAsync();
item.Name = "Tea";
await db.SaveChangesAsync(); // Updated: Item, by user-42
```

Registration: the same `AddDbContextWithHook<TDbContext>()` and `AddEfCoreAuditLogs<TDbContext, TPublisher>()` calls as the [Quick Start](index.md#-quick-start) — the latter registers `TPublisher` as a keyed scoped `IAuditLogPublisher` and attaches `EfCoreAuditHook` as a keyed scoped hook for `TDbContext`.

![EfCoreAuditHook captures eligible tracked states into AuditLogEntry records before EF saves, keeping keys and changed scalar values.](diagrams/capture-changes.svg)

`AuditLogEntry` contains `Action`, `EntityName`, `Keys`, `Changes` and the entity's `CreatedBy`, `CreatedOn`, `UpdatedBy`, `UpdatedOn`. Each `AuditFieldChange` has `FieldName`, `OldValue`, `NewValue`; composite primary keys produce multiple `Keys` entries. `AuditLogAction` is `Created`, `Updated` or `Deleted`.

See [Stamp the current user](stamp-current-user.md) for the optional `ICurrentUserProvider`.

## ⚙️ Configuration reference

None — this page's capture behaviour is driven by the `behaviour` and `propertyPolicy` options described in [Redact sensitive values](redact-sensitive-values.md#-configuration-reference); it exposes no option or override point of its own.

## 🧱 Where it fits

None — no other DKNet package calls this capture path; it runs through the shared [Hooks](../DKNet.EfCore.Hooks/index.md) snapshot pipeline shown in the [package architecture](index.md#-architecture), and only your own `IAuditLogPublisher` reads its output.

Only entities implementing [`IAuditedProperties`](../DKNet.EfCore.Abstractions/track-audit-fields.md) are eligible; deriving from `AuditedEntity` is optional.

**Entry point in your app:** register [AuditLogs](index.md#-quick-start) on your `DbContext`; capture runs automatically on every `SaveChangesAsync`.

## ⚠️ Gotchas & limits

- Creates have an empty `Changes` list. Modified entries include fields marked modified or whose values differ; deletes capture old scalar values with `NewValue = null`.
- Keys are captured before the database write. A database-generated identity can still be a temporary value in the audit entry.
- A failed save does not publish its pending batch; the next before-save pass drops a stale batch for that context.
- Use `SaveChangesAsync`. The underlying Hooks interceptor rejects synchronous saves unless hooks are disabled.
