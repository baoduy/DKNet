# Stamp the current user

Part of [DKNet.EfCore.AuditLogs](index.md). Implement `ICurrentUserProvider` and register it as in the Quick Start. For example, replace `DemoUser` with:

```csharp
public sealed class DemoUser : ICurrentUserProvider
{
    public string? GetCurrentUser() => "service-import";
}
```

![EfCoreAuditHook reads the current user before building entries, preserving explicit audit fields and deferring to the ownership key when no user is available.](diagrams/stamp-current-user.svg)

`AddCurrentUserProvider<TDbContext, TProvider>()` registers the provider scoped and unkeyed: the first provider registration wins application-wide. The call attaches the audit hook even without a publisher and only adds default audit options when none exist.

## ⚙️ Configuration reference

| Save state | Behavior with a non-empty user |
|---|---|
| Added | When `CreatedBy` is blank, fill it and set `CreatedOn` to UTC now. When `CreatedBy` is already set, leave both unchanged. |
| Modified | Fill `UpdatedBy`/`UpdatedOn` unless either was explicitly changed in this tracked change set. |
| Deleted | Leave audit identity fields unchanged. |

A missing provider or `null`/empty user skips user stamping. No arguments or configuration section are read by this registration.

## 🧱 Where it fits

See [ownership and the audit-field split](../DKNet.EfCore.DataAuthorization/stamp-ownership.md#who-fills-createdbyupdatedby-the-audit-field-split). With a non-empty user, `EfCoreAuditHook` owns the audit fields and `DataOwnerHook` owns `OwnedBy`; otherwise ownership supplies the fallback. The choice comes from provider values rather than registration order.

## ⚠️ Gotchas & limits

- Return a stable subject identifier: the user string is published unmasked as audit metadata.
- A later call for a different DbContext attaches its hook but does not replace the application-wide provider.
- Explicit domain audit values survive: non-empty `CreatedBy` wins on creation, and a changed modifier or modification timestamp wins on update.
