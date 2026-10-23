# Capture entity changes

Part of [DKNet.EfCore.AuditLogs](index.md). Use the [Quick Start](index.md#-quick-start) context and publisher. After its first save, update the tracked item:

```csharp
var item = await db.Items.SingleAsync();
item.Name = "Tea";
await db.SaveChangesAsync(); // Updated: Item, by user-42
```

![EfCoreAuditHook captures eligible tracked states into AuditLogEntry records before EF saves, keeping keys and changed scalar values.](diagrams/capture-changes.svg)

`AuditLogEntry` contains `Action`, `EntityName`, `Keys`, `Changes` and the entity's `CreatedBy`, `CreatedOn`, `UpdatedBy`, `UpdatedOn`. Each `AuditFieldChange` has `FieldName`, `OldValue`, `NewValue`; composite primary keys produce multiple `Keys` entries. `AuditLogAction` is `Created`, `Updated` or `Deleted`.

## 🧱 Where it fits

The [package architecture](index.md#-architecture) shows the shared Hooks snapshot pipeline. Only entities implementing `IAuditedProperties` are eligible; deriving from `AuditedEntity` is optional.

## ⚠️ Gotchas & limits

- Creates have an empty `Changes` list. Modified entries include fields marked modified or whose values differ; deletes capture old scalar values with `NewValue = null`.
- Keys are captured before the database write. A database-generated identity can still be a temporary value in the audit entry.
- A failed save does not publish its pending batch; the next before-save pass drops a stale batch for that context.
- Use `SaveChangesAsync`. The underlying Hooks interceptor rejects synchronous saves unless hooks are disabled.
