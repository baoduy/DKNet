# DKNet.EfCore.Encryption

Encrypt and decrypt EF Core string columns through value converters for .NET developers.

## ✨ Why use it?

- Keep selected database columns unreadable as plaintext while entities expose normal strings.
- Detect ciphertext tampering through AES-GCM authentication.
- Supply your own key source without adopting the Hooks pipeline.

## 🚀 Quick Start

Use a .NET 10 console project. Install the packages, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n EfCoreDemo
cd EfCoreDemo
dotnet add package DKNet.EfCore.Encryption
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.12
```

```csharp
using System.Security.Cryptography;
using DKNet.EfCore.Encryption;
using DKNet.EfCore.Encryption.Attributes;
using DKNet.EfCore.Encryption.Encryption;
using DKNet.EfCore.Encryption.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
connection.Open();
var services = new ServiceCollection();
services.AddEfCoreEncryption<DemoKey>();
services.AddDbContext<DemoDb>(o => o.UseSqlite(connection));
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
await using var db = scope.ServiceProvider.GetRequiredService<DemoDb>();
await db.Database.EnsureCreatedAsync();
db.Add(new Customer { Secret = "demo-value" });
await db.SaveChangesAsync();
db.ChangeTracker.Clear();
Console.WriteLine((await db.Customers.SingleAsync()).Secret); // demo-value
using var command = connection.CreateCommand();
command.CommandText = "SELECT Secret FROM Customers";
Console.WriteLine((string)command.ExecuteScalar()! != "demo-value"); // True

public sealed class DemoKey : IEncryptionKeyProvider
{
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
    public byte[] GetKey(Type entityType) => _key;
}
public sealed class DemoDb(DbContextOptions<DemoDb> options, IEncryptionKeyProvider keys) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseColumnEncryption(keys);
    }
}
public sealed class Customer
{
    public int Id { get; set; }
    [Encrypted]
    public string? Secret { get; set; }
}
```

Run with `dotnet run`.

## 🏗️ Architecture

![OnModelCreating uses the key provider to attach AES-GCM ColumnEncryptionConverter instances to marked strings, so EF writes ciphertext and materializes plaintext.](diagrams/components.svg)

Only EF Core is referenced by this package. `AddEfCoreEncryption` registers a singleton key provider; converters and their provider capture keys when the EF model is built. There is no SaveChanges hook or key-vault client.

## 🔄 How it works

![UseColumnEncryption selects marked string properties, rejects marked complex-type, non-string and key members, obtains entity keys and installs converters that encrypt writes and decrypt reads.](diagrams/efcore-encryption-column-path.svg)

The demo uses an ephemeral key only for its in-memory database. Persisted databases need a stable protected key across restarts. Null and empty strings pass through, while malformed or unauthenticated ciphertext throws.

## 🧩 Features

- [Encrypt mapped columns](encrypt-columns.md) — Attach converters and understand ciphertext and query limits.
- [Supply encryption keys](supply-encryption-keys.md) — Register stable keys and plan model and data changes.
- [Use a custom encryption provider](use-custom-provider.md) — Apply an explicit converter for another algorithm.

## 🔗 Related packages

- [DKNet.EfCore.AuditLogs](../DKNet.EfCore.AuditLogs/index.md) — use it to record changes; EncryptedAttribute values are automatically redacted there.
- [DKNet.EfCore.DataAuthorization](../DKNet.EfCore.DataAuthorization/index.md) — use it to limit row visibility.
- [DKNet.EfCore.Extensions](../DKNet.EfCore.Extensions/index.md) — use it for model conventions; column encryption is an independent model-building call.
- [DKNet.Svc.Encryption](../DKNet.Svc.Encryption/index.md) — use it for application cryptography outside EF column conversion.
