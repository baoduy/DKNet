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

## ⚠️ Upgrading from 13.3.0 or earlier

`UseColumnEncryption` now throws `InvalidOperationException` at model build when `[Encrypted]` sits on a complex-type member, a complex property or a mapped member that is not a `string`. Earlier releases skipped those marks and stored the values as plaintext. Remove the attribute, or move the field to an entity or owned type as a `string` property and encrypt its stored values outside EF Core first: see [Gotchas & limits](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Encryption/encrypt-columns.md#-gotchas--limits).

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Encryption/index.md) | Start with architecture and the end-to-end flow. |
| [Encrypt mapped columns](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Encryption/encrypt-columns.md) | Attach converters and understand ciphertext and query limits. |
| [Supply encryption keys](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Encryption/supply-encryption-keys.md) | Register stable keys and plan model and data changes. |
| [Use a custom encryption provider](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Encryption/use-custom-provider.md) | Apply an explicit converter for another algorithm. |
