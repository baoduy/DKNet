# Read and delete entities through specifications

Part of [DKNet.AspCore.Extensions](index.md). These mappers call `IRepositorySpec` directly. Register the repository and a relational `DbContext` before serving them. This example maps routes without sending database requests:

```csharp
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.EfCore.Abstractions.Entities;
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var group = app.MapGroup("/items");
group.MapGetById<Item, int, ItemModel>();
group.MapGetList<Item, int, ItemModel>("/");
group.MapDeleteById<Item, int>();
Console.WriteLine("Item routes mapped; register AddSpecRepo<TDbContext>() before serving.");
public sealed class Item : IEntity<int> { public int Id { get; set; } }
public sealed class ItemModel { public int Id { get; set; } }
```

![The delete mapper looks up an entity, returns 404 when absent, or deletes and saves through the repository with a 409 save-failure branch.](diagrams/entity-delete.svg)

`MapGetById<TEntity,TKey,TModel>` defaults to `{id}` and returns a projected model (200) or 404. `MapGetList<TEntity,TKey,TModel>` defaults to `/` and returns a paged projection or 400 for an unusable list query. `MapDeleteById<TEntity,TKey>` defaults to `{id}`, loads the entity, deletes through the repository and calls `SaveChangesAsync`; it returns 204, 404 for no match, or 409 on `DbUpdateException` from saving.

Each has a Guid-key shorthand omitting `TKey`. Explicit keys must implement `IEquatable<TKey>` and be bindable by minimal APIs; models must be classes. The three-type `MapDeleteById<TEntity,TKey,TRequest>` binds a class implementing `Fluents.Requests.IWithKey<TKey>` with `[AsParameters]`. It lets a host validation filter refuse a delete before the shared save helper runs, without adding a request body.

Deletes use the repository save pipeline, including any configured audit/domain-event hooks. They are hard deletes, subject to the repository and database behavior.

## ⚙️ Configuration reference

None — these mappers read no options of their own and expose no override point. List inputs are governed by `ListQueryOptions`; see [Filter, search and page list endpoints](query-list-endpoints.md) for its configuration reference and defaults.

## 🧱 Where it fits

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| `Map{Entity}Crud` (generated) | [SlimBus Generators](../DKNet.SlimBus.Generators/map-crud-endpoints.md) | Calls `MapGetById`, `MapGetList` and `MapDeleteById` to register the read and delete routes it generates for an entity. |

These mappers call `IRepositorySpec` from [EF Core Specifications](../DKNet.EfCore.Specifications/query-and-save.md) directly — register it with `AddSpecRepo<TDbContext>()` before serving them. For list inputs see [Filter, search and page list endpoints](query-list-endpoints.md).

**Entry point in your app:** calling `group.MapGetById<...>()`, `MapGetList<...>()` or `MapDeleteById<...>()` on a `RouteGroupBuilder` in `Program.cs` or inside an `IEndpointConfig.Map`.

## ⚠️ Gotchas & limits

- A validation request does not register a validator or validation filter. Attach the host’s validation mechanism through `ConfigureGroup`.
- Read/list calls need projection configuration appropriate to `TModel`.
- The compile-time key constraint does not guarantee HTTP binding; an unsupported key shape fails endpoint construction.
- A save `DbUpdateException` becomes a bare 409; this helper does not map its individual database cause to an error code.
