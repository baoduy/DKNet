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

## ⚙️ Configuration reference

| Argument | Type | Default | Effect |
|---|---|---|---|
| `createdOn` / `updatedOn` | `DateTimeOffset?` | `null` | Use `DateTimeOffset.UtcNow` when accepted. |

## 🧱 Where it fits

AuditedEntity stores domain-supplied identity. AuditLogs can supply the signed-in user on save; DataAuthorization supplies ownership-based audit identity when no current-user value is available. Those hooks run in the host, rather than in the entity base class.

![The package component map shows the host, contracts and consumers involved in this capability.](diagrams/components.svg)

## ⚠️ Gotchas & limits

- `SetCreatedBy` returns immediately when `CreatedBy` is already nonempty, before validating the new user.
- `SetUpdatedBy` ignores an explicitly older timestamp before validating the user. Otherwise blank users throw `ArgumentException`; omitted times use UTC now.
- The base class does not stamp saves. Use the AuditLogs or DataAuthorization hook for infrastructure-supplied values.
