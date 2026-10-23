# Expose an optimistic concurrency token

Part of [DKNet.EfCore.Abstractions](index.md). Implement `IConcurrencyEntity<TType>` to give infrastructure a consistent `RowVersion` property and setter.

Use the console-project setup in the [Quick Start](index.md#-quick-start), then replace `Program.cs` with this example.

```csharp
using DKNet.EfCore.Abstractions.Entities;

var account = new Account();
account.SetRowVersion([1, 2, 3]);
Console.WriteLine(account.RowVersion!.Length); // 3

public sealed class Account : Entity, IConcurrencyEntity<byte[]>
{
    public byte[]? RowVersion { get; private set; }
    public void SetRowVersion(byte[] version) => RowVersion = version;
}
```

![Account exposes a byte-array concurrency token and stores a supplied RowVersion through SetRowVersion.](diagrams/declare-concurrency.svg)

The interface decorates `RowVersion` with `[Timestamp]`, `[Column(Order = 1000)]` and `[IgnoreAuditLog]`. The implementation owns storage; the interface has no setter implementation.

## 🧱 Where it fits

The domain exposes IConcurrencyEntity; Extensions reads it when applying DefaultEntityTypeConfiguration. The configured EF provider performs conflict checks when the host saves.

![The package component map shows the host, contracts and consumers involved in this capability.](diagrams/components.svg)

## ⚠️ Gotchas & limits

- The contract alone does not perform a database conflict check. Configure the concrete property with EF Core.
- [Default entity conventions](../DKNet.EfCore.Extensions/configure-model.md) recognize the interface and configure a generated row-version concurrency token.
