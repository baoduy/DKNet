# Query and save through a repository

Part of [DKNet.EfCore.Specifications](index.md). Use the Quick Start repository and modify its tracked write target:

```csharp
var product = await db.Products.SingleAsync(p => p.Name == "Coffee");
product.Price = 15;
await repo.SaveChangesAsync();
Console.WriteLine(await repo.CountAsync(new MinimumPrice(10))); // 1
```

![IRepositorySpec stages tracked changes on its DbContext, then SaveChangesAsync discovers new navigation entities and calls the concurrency-aware save helper.](diagrams/query-and-save.svg)

| Operation | Use |
|---|---|
| `Query<TEntity>(spec)` | Deferred entity query; inspect SQL with `ToQueryString()`. |
| `AnyAsync`, `CountAsync` | Existence or count after applying the specification. |
| `FirstAsync`, `FirstOrDefaultAsync`, `ToListAsync` | Materialize entities; First throws on an empty set, FirstOrDefault returns null. |
| `AddAsync`, `AddRangeAsync` | Stage inserts; save explicitly afterward. |
| `Entry<TEntity>` | Access the EF tracked entry. |
| `UpdateAsync`, `UpdateRangeAsync` | Mark entities modified and discover new navigation entities; save explicitly. |
| `Delete` | Stage a tracked deletion. |
| `BulkDeleteAsync<TEntity>` | ExecuteDeleteAsync immediately with a predicate. |
| `BeginTransactionAsync` | Begin a context database transaction; caller owns completion/disposal. |
| `SaveChangesAsync` | Add newly discovered navigation entities and invoke concurrency handling. |

`AddSpecRepo<TDbContext>()` uses `TryAddScoped` for the repository and `TryAddSingleton` for `IRepositorySpecFactory`. The first repository registration wins, even if a later call names another context.

## ⚙️ Configuration reference

For background work, register an `IDbContextFactory<DemoDb>` (for example through `AddDbContextFactory`) before creating a provider:

```csharp
var factory = provider.GetRequiredService<IRepositorySpecFactory>();
await using var owned = factory.CreateAsync<DemoDb>();
var count = await owned.Repository.CountAsync(new MinimumPrice(10));
Console.WriteLine(count);
```

Despite its name, `CreateAsync<TDbContext>()` returns `IRepositorySpecProvider` synchronously, not a Task. Disposing it disposes its context and DI scope.

## 🧱 Where it fits

The [repository architecture](index.md#-architecture) shows query and persistence paths. Registered context interceptors still run during tracked saves. The save helper resolves a keyed `IEfCoreExceptionHandler` when available; this package does not install one — see [Customise how concurrency conflicts are resolved](../DKNet.EfCore.Extensions/customise-concurrency-handling.md) to register one.

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| Generated CRUD handlers | [DKNet.SlimBus.Generators](../DKNet.SlimBus.Generators/generate-handlers.md) | A generated `[CrudCreate]` handler calls `IRepositorySpec.AddAsync`; a generated update/action handler calls `FirstOrDefaultAsync` to load the entity, then mutates it directly (no explicit `UpdateAsync` call — the auto-save interceptor persists the tracked change). |
| `MapGetById`, `MapGetList`, `MapDeleteById` | [DKNet.AspCore.Extensions](../DKNet.AspCore.Extensions/map-entity-endpoints.md) | Inject `IRepositorySpec` into their mapped minimal-API endpoints to query and persist directly. |

**Entry point in your app:** inject `IRepositorySpec` (or resolve it through `IRepositorySpecFactory`) wherever your own code needs to query or save.

## ⚠️ Gotchas & limits

- Keep the context/scope alive while materializing a deferred query or enumerating results.
- `UpdateAsync` does not commit; its integer result counts newly discovered navigation entities, not saved rows.
- `BulkDeleteAsync` bypasses SaveChanges and its audit/event/ownership hooks. It is not a per-entity Delete loop.
- The factory path requires IDbContextFactory registration; AddSpecRepo alone does not create that service.
- See the [migration guide](../EfCore/Migrating-Repos-To-Specifications.md) for the removed `IRepository<T>` surface; those packages no longer ship.
