# Filter owned rows

Part of [DKNet.EfCore.DataAuthorization](index.md). In the [Quick Start](index.md#-quick-start), `IDataOwnerDbContext.AccessibleKeys` forwards the provider keys. Override the provider to grant several keys:

```csharp
public sealed class DemoOwner : IDataOwnerProvider
{
    public string? GetOwnershipKey() => "tenant-a";
    public ICollection<string> GetAccessibleKeys() => ["tenant-a", "tenant-b"];
}
```

![DataOwnerAuthQuery checks each IOwnedBy hierarchy root, rejects unfilterable models, and applies the context unrestricted flag or accessible-key membership on each query.](diagrams/filter-owned-rows.svg)

The predicate is `IsUnrestrictedAccess || AccessibleKeys.Contains(entity.OwnedBy)`. Empty keys deny every owned row unless unrestricted access is explicitly enabled. `DataOwnerAuthQuery.IsIgnorable` is `false`, so a specification's `IgnoreQueryFilters()` does not bypass it.

## ⚙️ Configuration reference

| Setting | Type | Default | Effect |
|---|---|---|---|
| `AccessibleKeys` | `IEnumerable<string>` | Required implementation | Keys allowed on reads. |
| `IsUnrestrictedAccess` | `bool` | `false` | Explicit context-wide read-filter bypass. |
| `GetAccessibleKeys()` | `ICollection<string>` | One ownership key, or empty for null/empty key | Source your context can forward; also used by the save guard. |

`AddDataOwnerProvider<TDbContext, TProvider>()` requires `TDbContext : DbContext, IDataOwnerDbContext`. It registers a global model builder and scoped provider, then the keyed save hook. Call `UseAutoConfigModel<TDbContext>()` on options to apply the builder.

## 🧱 Where it fits

The [ownership architecture](index.md#-architecture) shows the read and write paths; [the audit-field split](stamp-ownership.md#who-fills-createdbyupdatedby-the-audit-field-split) explains which provider fills identity fields. The filter is installed on hierarchy roots only, covering TPH, TPT and TPC descendants. A derived `IOwnedBy` type below a root that does not implement `IOwnedBy` throws during model building: move the contract to the root.

## ⚠️ Gotchas & limits

- Omitting `UseAutoConfigModel` leaves the filter unapplied without an error. Check the complete registration, not just the provider.
- Model builders are registered process-wide. Another context using auto configuration and owned entities must also implement `IDataOwnerDbContext`, or model building throws.
- Keep `AccessibleKeys` declared as `IEnumerable<string>`, matching the interface and the query translation path.
- The filter does not authorize raw SQL or writes that bypass the tracker. `IsUnrestrictedAccess` changes reads, not the reassignment guard.
- The source records an SQL-shape trade-off: the unrestricted `OR` branch can hinder owner-column index use, and varying key lists can change SQL and query plans. Measure your provider and workload; this package does not promise an index seek.
