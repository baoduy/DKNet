# DKNet.EfCore.DataAuthorization

A .NET library for secure, multi-tenant data authorization in Entity Framework Core. Enables automatic filtering of data
based on ownership, supports DDD and API scenarios, and is designed for extensibility.

## Features

- Automatic global query filtering based on data ownership keys
- Interfaces for associating entities with owners (`IOwnedBy`), providing ownership keys (`IDataOwnerProvider`), and
  integrating with DbContext (`IDataOwnerDbContext`)
- Extension methods for easy setup via dependency injection (`EfCoreDataAuthSetup`)
- Support for multi-tenancy and secure data isolation
- .NET 9.0 compatible

## Installation

Add the NuGet package to your project:

```
dotnet add package DKNet.EfCore.DataAuthorization
```

## Usage

Register the data authorization provider and setup in your DI container:

```csharp
using Microsoft.Extensions.DependencyInjection;
using DKNet.EfCore.DataAuthorization;

services.AddAutoDataKeyProvider<MyDbContext, MyOwnerProvider>();
```

Implement `IOwnedBy` on your entities:

```csharp
public class Document : IOwnedBy
{
    public string? OwnedBy { get; private set; }
    public void SetOwnedBy(string ownerKey) => OwnedBy = ownerKey;
}
```

Implement `IDataOwnerProvider` to supply ownership keys and accessible keys:

```csharp
public class MyOwnerProvider : IDataOwnerProvider
{
    public IEnumerable<string> GetAccessibleKeys() => ...; // e.g., from user context
    public string GetOwnershipKey() => ...; // e.g., current user's tenant key
}
```

## API

- `EfCoreDataAuthSetup.AddAutoDataKeyProvider<TDbContext, TProvider>(IServiceCollection)`: Registers automatic data key
  management for a DbContext.
- `IOwnedBy`: Interface for entities supporting ownership, with methods to get/set the owner key.
- `IDataOwnerProvider`: Interface for providing accessible keys and ownership key for new entities.
- `IDataOwnerDbContext`: Interface for DbContexts supporting data authorization, exposing accessible keys.

## License

MIT © 2026 drunkcoding

## Repository

[https://github.com/baoduy/DKNet](https://github.com/baoduy/DKNet)

## Contributing

Pull requests and issues are welcome!

