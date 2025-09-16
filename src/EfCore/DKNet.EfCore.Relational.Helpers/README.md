# DKNet.EfCore.Relational.Helpers

A .NET library providing extension methods and helpers for Entity Framework Core’s relational features. Simplifies database connection management, table metadata access, and existence checks for EF Core applications.

## Features
- Get and open the underlying `DbConnection` from a `DbContext`
- Retrieve schema and table names for entity types
- Check if a table for a given entity exists in the database
- Supports SQL Server and other EF Core relational providers
- .NET 9.0 compatible

## Installation
Add the NuGet package to your project:

```
dotnet add package DKNet.EfCore.Relational.Helpers
```

## Usage

```csharp
using DKNet.EfCore.Relational.Helpers;

// Get and open the database connection
var conn = await dbContext.GetDbConnection();

// Get schema and table name for an entity
var (schema, tableName) = dbContext.GetTableName<MyEntity>();

// Check if the table exists
bool exists = await dbContext.TableExistsAsync<MyEntity>();
```

## API
- `GetDbConnection(this DbContext dbContext, CancellationToken cancellationToken = default)`: Returns an open `DbConnection`.
- `GetTableName<TEntity>(this DbContext dbContext)`: Returns the schema and table name for an entity type.
- `TableExistsAsync<TEntity>(this DbContext dbContext, CancellationToken cancellationToken = default)`: Checks if the table for an entity exists.

## License
MIT © 2026 drunkcoding

## Repository
[https://github.com/baoduy/DKNet](https://github.com/baoduy/DKNet)

## Contributing
Pull requests and issues are welcome!

