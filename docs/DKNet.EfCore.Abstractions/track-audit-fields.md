# Record who created and changed an entity

Part of [DKNet.EfCore.Abstractions](index.md). Derive from `AuditedEntity<TKey>` or its Guid specialization to store audit identity and timestamps.

Use the console-project setup in the [Quick Start](index.md#-quick-start), then replace `Program.cs` with this example.

```csharp
using DKNet.EfCore.Abstractions.Entities;

var invoice = new Invoice("alice");
invoice.MarkPaid("bob");
Console.WriteLine(invoice.LastModifiedBy); // bob

public sealed class Invoice : AuditedEntity
{
    public Invoice(string user) => SetCreatedBy(user);
    public void MarkPaid(string user) => SetUpdatedBy(user);
}
```

![Invoice calls SetCreatedBy once and SetUpdatedBy on payment, then LastModifiedBy returns the latest user.](diagrams/track-audit-fields.svg)

`IAuditedEntity<TKey>` combines identity with `IAuditedProperties`: `CreatedBy`, `CreatedOn`, `UpdatedBy` and `UpdatedOn`. `LastModifiedBy` and `LastModifiedOn` fall back to creation values and are not mapped.

None — no DI registration needed; `AuditedEntity<TKey>` is a base class you derive from, and `IAuditedProperties`/`IAuditedEntity<TKey>` are interfaces you can implement directly instead.

## ⚙️ Configuration reference

| Argument / Override point | Type | Default | Effect |
|---|---|---|---|
| `createdOn` / `updatedOn` | `DateTimeOffset?` | `null` | Use `DateTimeOffset.UtcNow` when accepted. |
| `IAuditedProperties` | Interface | — | Implement it yourself (without `AuditedEntity<TKey>`) to expose `CreatedBy`, `CreatedOn`, `UpdatedBy`, `UpdatedOn` on your own base type. |

## 🧱 Where it fits

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| `AuditLogExtensions` / `EfCoreAuditHook` | [DKNet.EfCore.AuditLogs](../DKNet.EfCore.AuditLogs/capture-changes.md) | Reads `CreatedBy`/`CreatedOn`/`UpdatedBy`/`UpdatedOn` to build each `AuditLogEntry`, and [stamps the current user](../DKNet.EfCore.AuditLogs/stamp-current-user.md) into them before capture. |
| `DataOwnerHook` | [DKNet.EfCore.DataAuthorization](../DKNet.EfCore.DataAuthorization/stamp-ownership.md) | Fills the same fields from the ownership key when no current-user value is available (see the [audit-field split](../DKNet.EfCore.DataAuthorization/stamp-ownership.md#who-fills-createdbyupdatedby-the-audit-field-split)). |
| `FluentsEntityEndpointMapperExtensions` / `ListQuery` | [DKNet.AspCore.Extensions](../DKNet.AspCore.Extensions/index.md) | Reads `IAuditedProperties`/`IAuditedEntity<TKey>` to offer activity-window filtering on generated list endpoints. |
| `DefaultEntityTypeConfiguration` | [DKNet.EfCore.Extensions](../DKNet.EfCore.Extensions/configure-model.md) | Recognizes the audited contracts while applying default entity model configuration. |

**Entry point in your app:** derive from `AuditedEntity<TKey>` (or implement `IAuditedProperties` yourself); register [AuditLogs](../DKNet.EfCore.AuditLogs/index.md) and/or [DataAuthorization](../DKNet.EfCore.DataAuthorization/index.md) hooks to fill the fields automatically.

![The package component map shows the host, contracts and consumers involved in this capability.](diagrams/components.svg)

## ⚠️ Gotchas & limits

- `SetCreatedBy` returns immediately when `CreatedBy` is already nonempty, before validating the new user.
- `SetUpdatedBy` ignores an explicitly older timestamp before validating the user. Otherwise blank users throw `ArgumentException`; omitted times use UTC now.
- The base class does not stamp saves. Use the AuditLogs or DataAuthorization hook for infrastructure-supplied values.
