# Stamp and guard ownership

Part of [DKNet.EfCore.DataAuthorization](index.md). Use the [Quick Start](index.md#-quick-start). Its tracked invoice exposes a domain method; try an inaccessible transfer after reading it:

```csharp
invoice.AssignOwner("tenant-z");
await db.SaveChangesAsync();
Console.WriteLine(invoice.OwnedBy); // tenant-a: reverted without throwing
```

![DataOwnerHook sets a blank owner on added rows and checks modified ownership against accessible keys, reverting blank or inaccessible replacements before saving.](diagrams/stamp-ownership.svg)

`IOwnedBy` exposes a getter-only `string OwnedBy`; EF-mapped accessors can reach a private setter. An existing non-empty owner on an added entity is preserved. A modified owner is accepted only when unchanged or non-empty and present in `GetAccessibleKeys()`.

## ⚙️ Configuration reference

| Setting | Type | Default | Effect |
|---|---|---|---|
| `GetOwnershipKey()` | `string?` | Required implementation | Non-empty value stamps blank new owners; null/empty skips creation stamping and audit fallback. |
| `GetAccessibleKeys()` | `ICollection<string>` | See [read configuration](filter-owned-rows.md#-configuration-reference) | Allowed ownership replacements on modified entries. |

### Who fills `CreatedBy`/`UpdatedBy`: the audit-field split

| Current user for the save | `OwnedBy` | Audit fields on `IAuditedProperties` |
|---|---|---|
| Provider absent or returns null/empty | Ownership hook | Ownership-key fallback. |
| Non-empty `ICurrentUserProvider` value | Ownership hook | `EfCoreAuditHook` supplies the signed-in user. |

Both hooks decide from provider values, independently of registration order. A blank creation identity is filled with UTC time; explicit creation values and modifier/timestamp changes are preserved by the shared `AuditPropertyStamper`. Audit fallback also applies to audited entries without `IOwnedBy`; the query filter only targets owned types.

## 🧱 Where it fits

[Stamp the current user](../DKNet.EfCore.AuditLogs/stamp-current-user.md) describes the optional audit provider. The [package architecture](index.md#-architecture) shows this composition. `AddDbContextWithHook` attaches the runner; plain `AddDbContext` needs explicit hook wiring.

## ⚠️ Gotchas & limits

- Reassignment is reverted rather than rejected, so a successful save is not proof a requested transfer happened.
- The guard checks the tracked original value; it is not a database constraint or a check that an attached entity was originally accessible. Authorize write targets in your application.
- A blank ownership key can leave a new row unowned and hidden by the read filter. Existing non-empty owners on new rows are not validated by this hook.
- `ExecuteUpdate`, `ExecuteDelete`, raw SQL and other tracker-bypassing writes do not run this hook. Use async tracked saves for stamping.
