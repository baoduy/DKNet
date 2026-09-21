---
name: dknet-packages
description: Routes a .NET 10 DDD/Onion Architecture need to the right DKNet NuGet package and to the sibling skill that teaches it, without teaching any package's API in depth. Covers which DKNet.* package to `dotnet add package`, the canonical Program.cs wiring order for a new API (AddDbContextWithHook, AddSpecRepo, AddEventPublisher/AddSlimBusEventPublisher, AddSlimBusEfCoreInterceptor, AddIdempotencyWithRedisStore/MsSqlStore/NpgsqlStore, UseEndpointConfigs), the four DI registration conventions, the onion rings and package dependency graph, and every removed/renamed API (DKNet.EfCore.Repos and DKNet.EfCore.Repos.Abstractions do not exist, AddDKNet() does not exist, there is no AggregateRoot type - use Entity<TKey>/AuditedEntity). Use when starting a new project with DKNet, deciding which DKNet package solves a scenario, ordering Program.cs registrations, or migrating off DKNet.EfCore.Repos.
license: MIT
metadata:
  author: baoduy
  version: "0.1.0"
  packages: "all"
---

# DKNet package router and setup order

DKNet is 29 independent .NET 10 packages (28 published to NuGet, 1 source-only) for DDD + Onion Architecture — there is no `AddDKNet()` and no aggregator. This skill routes a need to the right package and the sibling skill that teaches it, and gives the setup order, DI conventions, and removed/renamed APIs that span every package. It does not teach any package's API in depth — read the owning skill (last column below) before writing code against it. Full detail lives in `references/package-catalog.md`, `references/removed-and-renamed.md`, and `references/namespace-imports.md`.

## Packages

Every ring of the onion is a separate package; every dependency points inward, toward `DKNet.EfCore.Abstractions` or a foundation package. No cycle exists.

| Ring | Packages |
|---|---|
| Domain (core) | `DKNet.EfCore.Abstractions` |
| Application | `DKNet.SlimBus.Extensions`, `DKNet.Svc.*` |
| Infrastructure | `DKNet.EfCore.Extensions`, `.Specifications`, `.Hooks`, `.Events`, `.AuditLogs`, `.DataAuthorization`, `.Relational.Helpers` |
| Presentation | `DKNet.AspCore.*` |

`DKNet.AspCore.Extensions` is not application-ring-only: it carries a direct dependency on `DKNet.EfCore.Specifications` (infrastructure ring) for its `MapGetById`/`MapGetList`/`MapDeleteById` endpoint helpers. `DKNet.EfCore.Encryption` and the two Roslyn generators (`DKNet.EfCore.DtoGenerator`, `DKNet.SlimBus.Generators`) sit outside the rings entirely — no runtime `ProjectReference` to any other DKNet package. The "Depends on" column below is the real dependency graph, verified against every package's project file.

| Package | Install | What it gives you | Depends on | Reference |
|---|---|---|---|---|
| `DKNet.Fw.Extensions` | `dotnet add package DKNet.Fw.Extensions` | Reflection, type, string, enum helpers; assembly scanning | — | `dknet-core-utilities` |
| `DKNet.RandomCreator` | `dotnet add package DKNet.RandomCreator` | Cryptographically secure random strings/tokens | — | `dknet-core-utilities` |
| `DKNet.EfCore.Abstractions` | `dotnet add package DKNet.EfCore.Abstractions` | Entity base classes, domain-event queue, attributes | — | `dknet-efcore-domain-model` |
| `DKNet.EfCore.Extensions` | `dotnet add package DKNet.EfCore.Extensions` | Config discovery, global filters, seeding, GUID v7, sequences | Fw.Extensions, EfCore.Abstractions | `dknet-efcore-domain-model` |
| `DKNet.EfCore.Relational.Helpers` | `dotnet add package DKNet.EfCore.Relational.Helpers` | Table create/exists/connection/name across providers | EfCore.Extensions | `dknet-efcore-domain-model` |
| `DKNet.EfCore.Specifications` | `dotnet add package DKNet.EfCore.Specifications` | Query/persist via `IRepositorySpec`; dynamic predicates | EfCore.Extensions | `dknet-efcore-specifications` |
| `DKNet.EfCore.Hooks` | `dotnet add package DKNet.EfCore.Hooks` | Before/after-`SaveChanges` interceptor pipeline | Fw.Extensions, EfCore.Extensions | `dknet-efcore-save-pipeline` |
| `DKNet.EfCore.Events` | `dotnet add package DKNet.EfCore.Events` | Dispatches queued domain events after commit | EfCore.Abstractions, EfCore.Hooks | `dknet-efcore-save-pipeline` |
| `DKNet.EfCore.AuditLogs` | `dotnet add package DKNet.EfCore.AuditLogs` | Field-level change history, redaction | EfCore.Abstractions, EfCore.Hooks | `dknet-efcore-save-pipeline` |
| `DKNet.EfCore.DataAuthorization` | `dotnet add package DKNet.EfCore.DataAuthorization` | Row-level ownership filter + owner stamping | EfCore.Extensions, EfCore.Hooks, EfCore.AuditLogs | `dknet-efcore-data-security` |
| `DKNet.EfCore.Encryption` | `dotnet add package DKNet.EfCore.Encryption` | Transparent column encryption (`ValueConverter`) | — | `dknet-efcore-data-security` |
| `DKNet.EfCore.DtoGenerator` | `dotnet add package DKNet.EfCore.DtoGenerator` | Roslyn generator: DTOs from `[GenerateDto]` | — | `dknet-codegen` |
| `DKNet.SlimBus.Extensions` | `dotnet add package DKNet.SlimBus.Extensions` | CQRS on SlimMessageBus; auto-save; events onto the bus | EfCore.Events | `dknet-slimbus-cqrs` |
| `DKNet.SlimBus.Generators` | `dotnet add package DKNet.SlimBus.Generators` | Roslyn generator: CRUD slice from `[CrudAction]` | — | `dknet-codegen` |
| `DKNet.AspCore.Extensions` | `dotnet add package DKNet.AspCore.Extensions` | Endpoint groups, `.Response()`, contextual request binding | SlimBus.Extensions, EfCore.Specifications | `dknet-aspcore-api` |
| `DKNet.AspCore.Tasks` | `dotnet add package DKNet.AspCore.Tasks` | Runs an `IBackgroundTask` once at start-up | — | `dknet-aspcore-api` |
| `DKNet.AspCore.Idempotency` | `dotnet add package DKNet.AspCore.Idempotency` | Retry-safe endpoints; process-local store built in | Fw.Extensions | `dknet-idempotency` |
| `DKNet.AspCore.Idempotency.Relational` | `dotnet add package DKNet.AspCore.Idempotency.Relational` | Shared EF Core base the two relational stores derive from | AspCore.Idempotency | `dknet-idempotency` |
| `DKNet.AspCore.Idempotency.MsSqlStore` | `dotnet add package DKNet.AspCore.Idempotency.MsSqlStore` | SQL Server idempotency key store | AspCore.Idempotency, .Relational | `dknet-idempotency` |
| `DKNet.AspCore.Idempotency.NpgsqlStore` | `dotnet add package DKNet.AspCore.Idempotency.NpgsqlStore` | PostgreSQL idempotency key store | AspCore.Idempotency, .Relational | `dknet-idempotency` |
| `DKNet.AspCore.Idempotency.RedisStore` | `dotnet add package DKNet.AspCore.Idempotency.RedisStore` | Redis idempotency key store (`SET NX`, native TTL) | AspCore.Idempotency | `dknet-idempotency` |
| `DKNet.Svc.BlobStorage.Abstractions` | `dotnet add package DKNet.Svc.BlobStorage.Abstractions` | Provider-agnostic `IBlobService` contract | — | `dknet-blob-storage` |
| `DKNet.Svc.BlobStorage.AwsS3` | `dotnet add package DKNet.Svc.BlobStorage.AwsS3` | AWS S3 (and S3-compatible) adapter | Svc.BlobStorage.Abstractions | `dknet-blob-storage` |
| `DKNet.Svc.BlobStorage.AzureStorage` | `dotnet add package DKNet.Svc.BlobStorage.AzureStorage` | Azure Blob Storage adapter | Svc.BlobStorage.Abstractions | `dknet-blob-storage` |
| `DKNet.Svc.BlobStorage.Local` | `dotnet add package DKNet.Svc.BlobStorage.Local` | Local-filesystem adapter, path-traversal safe | Svc.BlobStorage.Abstractions | `dknet-blob-storage` |
| `DKNet.Svc.Encryption` | `dotnet add package DKNet.Svc.Encryption` | Explicit AES-GCM/RSA, HMAC/SHA, Base64 | — | `dknet-services` |
| `DKNet.Svc.PdfGenerators` | `dotnet add package DKNet.Svc.PdfGenerators` | Markdown/HTML to PDF via headless Chromium | — | `dknet-services` |
| `DKNet.Svc.Transformation` | `dotnet add package DKNet.Svc.Transformation` | Bracketed-token substitution in template strings | — | `dknet-services` |
| `Aspire.Hosting.ServiceBus` | ProjectReference (source-only, not on NuGet) | Azure Service Bus emulator for a local Aspire AppHost | — | `dknet-slimbus-cqrs` |

## Quick start

The smallest DDD-shaped API: entities, EF Core wiring, and one query through `IRepositorySpec`. Every other example in this skill reuses the types declared here.

```bash
dotnet add package DKNet.EfCore.Abstractions    # entity base classes, domain-event queue
dotnet add package DKNet.EfCore.Extensions      # config discovery, global filters, GUID v7
dotnet add package DKNet.EfCore.Specifications  # query/persist via IRepositorySpec
dotnet add package DKNet.SlimBus.Extensions     # CQRS handlers with automatic SaveChanges
```

```csharp
using DKNet.AspCore.Extensions;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.Specifications.Definitions;
using Microsoft.EntityFrameworkCore;

public class Product : AuditedEntity
{
    private Product() { } // EF Core

    public static Product Create(string name, decimal price, string createdBy)
    {
        var product = new Product { Name = name, Price = price, IsActive = true };
        product.SetCreatedBy(createdBy);
        return product;
    }

    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; }

    public void Deactivate(string updatedBy)
    {
        IsActive = false;
        SetUpdatedBy(updatedBy);
    }
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}

public sealed class ActiveProductsSpec : Specification<Product>
{
    public ActiveProductsSpec()
    {
        WithFilter(p => p.IsActive);
        AddOrderBy(p => p.Name);
    }
}

public sealed class ProductByIdSpec : Specification<Product>
{
    public ProductByIdSpec(Guid id) => WithFilter(p => p.Id == id);
}

public sealed class ConsoleEventPublisher : DefaultEventPublisher
{
    public override Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        Console.WriteLine(eventObj);
        return Task.CompletedTask;
    }
}

public sealed class ProductsEndpoints : IEndpointConfig
{
    public string GroupEndpoint => "/products";

    public void Map(RouteGroupBuilder group) =>
        group.MapGet("/", () => Results.Ok(Array.Empty<string>()));
}
```

Wire the `DbContext` and the specification repository — `UseAutoConfigModel` goes inside the options callback, never in `OnModelCreating`:

```csharp
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Specifications;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextWithHook<AppDbContext>(options => options
    .UseSqlServer(builder.Configuration.GetConnectionString("Default")!)
    .UseAutoConfigModel<AppDbContext>());

builder.Services.AddSpecRepo<AppDbContext>();

var app = builder.Build();
app.Run();
```

Query through the specification — one non-generic `IRepositorySpec` serves every entity type:

```csharp
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;

public sealed class Catalogue(IRepositorySpec repo)
{
    public Task<List<Product>> ActiveAsync(CancellationToken cancellationToken = default) =>
        repo.ToListAsync(new ActiveProductsSpec(), cancellationToken);
}
```

## Rules

1. **There is no `AddDKNet()` and no aggregator.** Install and register only the packages a given `DbContext`, API, or worker actually needs — nothing requires a companion "core" package.
2. **Route by problem, then read the owning skill.** Pick the package from the table above, then read its skill before writing code — this skill does not teach package internals.
3. **Wire a new API in this order:** `DbContext`(+Hook) -> Specifications -> optional cross-cutting hooks (events/audit/ownership, order-independent among themselves) -> SlimBus -> SlimMessageBus transport -> idempotency store -> endpoints. A step used out of order either no-ops silently (a hook registered before the DbContext step) or throws at first request (`.RequiredIdempotentKey()` with no store registered).
4. **Use `AddDbContextWithHook<TDbContext>()` whenever anything hooks `SaveChanges`** — domain events, audit logs, or ownership stamping. Plain `AddDbContext<TDbContext>` registers no interceptor, and those features attach but never fire, with no compile or runtime error.
5. **`UseAutoConfigModel<TContext>()` is a `DbContextOptionsBuilder<TContext>` extension**, not a `ModelBuilder` one. It belongs inside the `AddDbContext(WithHook)` options callback; using it in `OnModelCreating` fails to compile.
6. **Every package's DI call fits one of four shapes** — invent none of your own:

   | Shape | Example |
   |---|---|
   | Bound from config | `AddAzureStorageAdapter`, `AddS3BlobService`, `AddLocalDirectoryBlobService` — the only three that read `IConfiguration` |
   | Delegate (`Action<TOptions>`) | `AddIdempotentKey`, `AddTransformerService`, the `Action<AzureStorageOptions>` overload of `AddAzureStorageAdapter` |
   | Required argument | `AddAesGcmEncryption(base64Key)`, `AddRsaEncryption(privateKeyBase64)`, `AddIdempotencyWithMsSqlStore(connectionString)` |
   | Type parameter (resolved from DI) | `AddEfCoreEncryption<TKeyServiceImplementation>`, `AddDataOwnerProvider<TDbContext,TProvider>`, `AddEventPublisher<TDbContext,TImpl>`, `AddIdempotentKey<TStore>` |
7. **`DKNet.EfCore.Repos` and `DKNet.EfCore.Repos.Abstractions` do not exist.** `dotnet add package` will not find them. Use `DKNet.EfCore.Specifications` and `AddSpecRepo<TDbContext>()`.
8. **No `AggregateRoot` type ships.** An aggregate root is just an `Entity<TKey>` / `Entity` / `AuditedEntity<TKey>` / `AuditedEntity` you choose to treat as a consistency boundary.
9. **`AddDataOwnerProvider<TDbContext,TProvider>()` affects every `DbContext` in the process**, not only `TDbContext` — its filter lives in a static, process-wide model-builder list. A second context with `IOwnedBy` entities must also implement `IDataOwnerDbContext`, or it throws at model-build time.
10. **DKNet ships no validation pipeline and no SlimMessageBus transport provider.** Bring your own `IRequestHandlerInterceptor<TRequest,TResponse>` for validation, and pick a transport (`Memory`, Azure Service Bus, ...) yourself — `AddSlimMessageBus(...)` is plain SlimMessageBus API, not a DKNet call.

## How to

### Wire a new API end to end

When: starting a project that needs persistence, CQRS, and at least one retry-safe endpoint.

```csharp
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.RedisStore;
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Specifications;
using Microsoft.EntityFrameworkCore;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

var builder = WebApplication.CreateBuilder(args);

// 1. DbContext + hook interceptor + model build
builder.Services.AddDbContextWithHook<AppDbContext>(options => options
    .UseSqlServer(builder.Configuration.GetConnectionString("Default")!)
    .UseAutoConfigModel<AppDbContext>());

// 2. Querying/persistence through one non-generic IRepositorySpec
builder.Services.AddSpecRepo<AppDbContext>();

// 3 (skipped here, optional, order-independent once after step 1): AddEfCoreAuditLogs /
// AddCurrentUserProvider (dknet-efcore-save-pipeline), AddDataOwnerProvider (dknet-efcore-data-security).

// 4. CQRS auto-save + domain events forwarded onto the bus
builder.Services.AddSlimBusEventPublisher<AppDbContext>();
builder.Services.AddSlimBusEfCoreInterceptor<AppDbContext>();

// 5. SlimMessageBus itself - plain SlimMessageBus API, DKNet ships no transport provider
builder.Services.AddSlimMessageBus(mbb => mbb
    .AddJsonSerializer()
    .AddServicesFromAssembly(typeof(AppDbContext).Assembly)
    .AddChildBus("Memory", mem => mem.WithProviderMemory().AutoDeclareFrom(typeof(AppDbContext).Assembly)));

// 6. Idempotency, registered before any route uses it
builder.Services.AddIdempotencyWithRedisStore(builder.Configuration.GetConnectionString("Redis")!);

var app = builder.Build();

// 7. Endpoints - convention-discovered groups, then a route guarded against duplicate submission
app.UseEndpointConfigs(o => o.EnableVersioning = false);
app.MapPost("/products", () => Results.Accepted()).RequiredIdempotentKey();

app.Run();
```

Notes:
- Step 3's packages are each owned by a sibling skill; this skill only fixes where they slot in.
- `UseEndpointConfigs` throws at startup if `EnableVersioning` (default `true`) is left on without `AddApiVersioning()` having been called — disable it or call `AddApiVersioning()` first.

### Decide `AddDbContext` vs `AddDbContextWithHook`

When: a `DbContext` has no domain events, audit trail, or ownership filter.

```csharp
using DKNet.EfCore.Specifications;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// No hooks needed - AddDbContext is enough. Its callback hands you a plain DbContextOptionsBuilder,
// not the generic DbContextOptionsBuilder<AppDbContext>, so UseAutoConfigModel needs its
// Assembly[] overload here instead of the <TContext> one AddDbContextWithHook's callback allows.
builder.Services.AddDbContext<AppDbContext>(options => options
    .UseSqlServer(builder.Configuration.GetConnectionString("Default")!)
    .UseAutoConfigModel(new[] { typeof(AppDbContext).Assembly }));
builder.Services.AddSpecRepo<AppDbContext>();

var app = builder.Build();
app.Run();
```

Notes:
- The moment any feature needs `SaveChanges` hooked (events, audit, ownership), switch to `AddDbContextWithHook<TDbContext>`'s single-parameter overload (`options => ...`) and drop back to `UseAutoConfigModel<AppDbContext>()` — nothing else in this snippet changes.
- This is about which overload's callback you are in, not which method you called: `AddDbContextWithHook`'s *other* overload (`Action<IServiceProvider, DbContextOptionsBuilder>`, for reading a scoped service while configuring) hands you the same plain `DbContextOptionsBuilder` and hits the same `UseAutoConfigModel(Assembly[])` requirement.

### Add a cross-cutting hook after the fact

When: a DbContext is already wired and now needs domain events (or audit/ownership - same shape).

```csharp
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Specifications;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextWithHook<AppDbContext>(options => options
    .UseSqlServer(builder.Configuration.GetConnectionString("Default")!)
    .UseAutoConfigModel<AppDbContext>());
builder.Services.AddSpecRepo<AppDbContext>();

// Added later - AddEventPublisher only needs the hook runner AddDbContextWithHook already
// registered above; its position relative to AddSpecRepo does not matter.
builder.Services.AddEventPublisher<AppDbContext, ConsoleEventPublisher>();

var app = builder.Build();
app.Run();
```

Notes:
- `AddEventPublisher<TDbContext,TImplementation>()` lives in the ambient `Microsoft.Extensions.DependencyInjection` namespace — no `using DKNet.EfCore.Events;` needed.
- Depth on domain events (in-process vs `AddSlimBusEventPublisher`, `EventException`, `[RaisesEvent]`) is `dknet-efcore-save-pipeline`'s job, not this skill's.

### Migrate a query/write off `DKNet.EfCore.Repos`

When: existing code calls `IRepository<T>`/`IWriteRepository<T>`/`IReadRepository<T>` — all removed.

```csharp
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;

public sealed class ProductWriter(IRepositorySpec repo)
{
    public async Task DeactivateAsync(Guid id, string updatedBy, CancellationToken cancellationToken = default)
    {
        var product = await repo.FirstAsync(new ProductByIdSpec(id), cancellationToken);
        product.Deactivate(updatedBy);
        await repo.UpdateAsync(product, cancellationToken);
        await repo.SaveChangesAsync(cancellationToken);
    }
}
```

Notes:
- One injected `IRepositorySpec` replaces a repository per entity; `ISpecification<TEntity>` (here `ProductByIdSpec`) replaces the ad hoc filter expressions the old repositories took directly.
- The old `DeleteRange<TEntity>` is now `BulkDeleteAsync<TEntity>(predicate, ct)` — a server-side `ExecuteDeleteAsync`, not a load-then-remove. See `references/removed-and-renamed.md` for the rest of the old-API-to-new-API mapping.

### Choose and wire an idempotency store

When: a write endpoint must be safe for a client to retry.

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.NpgsqlStore;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
    builder.Services.AddIdempotentKey(); // process-local store: dev/test only, lost on restart
else
    builder.Services.AddIdempotencyWithNpgsqlStore(builder.Configuration.GetConnectionString("Idempotency")!);

var app = builder.Build();
app.MapPost("/api/orders", () => Results.Accepted()).RequiredIdempotentKey();
app.Run();
```

Notes:
- Swap `NpgsqlStore` for `MsSqlStore`/`RedisStore` per deployment target — the call shape (`AddIdempotencyWith{Store}(connectionString, config?)`) is the same across all three.
- Between two *named* stores, first registration wins and a second call's options are dropped, not merged — cover the registration order with a startup test rather than relying on call order alone.

## Runtime behaviour

1. **Startup**: `AddDbContextWithHook` registers one hook runner per `DbContext` type; every `AddHook`/`AddEfCoreAuditLogs`/`AddDataOwnerProvider`/`AddEventPublisher` call attaches to that runner. `AddIdempotentKey`/`AddIdempotencyWith*Store` register `IdempotencyOptions` with `ValidateOnStart()`, so a bad option value fails startup, not the first request. `UseEndpointConfigs` discovers every `IEndpointConfig` in the scanned assemblies and fails fast if `EnableVersioning` is on without `AddApiVersioning()` — before mapping anything.
2. **A write request**: the idempotency endpoint filter checks the key first (replays or rejects before the handler runs) -> the handler mutates the aggregate, which queues events but does not dispatch them -> `EfAutoSavePostInterceptor` calls `SaveChangesAsync` once the handler returns a non-failed result -> hooks run around that save (audit capture before, everything else after) -> queued domain events publish only once the save has actually committed. A failed save publishes nothing; a publisher that throws is logged and swallowed - the row still commits, the event is lost.

## Gotchas

- **`AddDbContextWithHook` is not automatic.** Plain `AddDbContext` skips the hook interceptor entirely — events/audit/ownership hooks attach but never fire, with no compile or runtime error.
- **`UseAutoConfigModel<TContext>()` only resolves when the options callback's parameter is already typed `DbContextOptionsBuilder<TContext>`.** Plain `AddDbContext<TContext>(options => ...)` and `AddDbContextWithHook`'s `Action<IServiceProvider, DbContextOptionsBuilder>` overload both hand you the non-generic `DbContextOptionsBuilder` instead — a real `CS1929` if you call the `<TContext>` overload there. Use `AddDbContextWithHook`'s single-parameter `Action<DbContextOptionsBuilder<TDbContext>>` overload, or fall back to `UseAutoConfigModel(new[] { typeof(TContext).Assembly })`.
- **`AddDataOwnerProvider`'s filter is process-static, not per-context.** It lives in a static model-builder list, so every `DbContext` calling `UseAutoConfigModel()` gets it, not only the one named in the call.
- **DataAuthorization fails closed.** A `DbContext` in scope of the filter that does not implement `IDataOwnerDbContext` throws at model-build time — that is intentional, not a bug to route around.
- **Two ways to raise a domain event, two different failure modes.** `AddEvent(instance)` needs no `IMapper`; `AddEvent<TEvent>()`/`[RaisesEvent]` do, and skipping that registration throws `EventException` at save (loud). A publisher that itself throws is logged and swallowed instead (silent either way, the event is lost).
- **Idempotency stores are first-registration-wins between two named stores.** A second call's differing options (a stricter `Expiration`, a `ScopeHmacSecret`) are silently dropped, not merged.
- **The bare `AddIdempotentKey()` store is memory-only, per process.** Fine for dev/test; on multiple instances each gets its own ledger, so the same key is processed once per instance, not once total.
- **`AddCurrentUserProvider` vs `AddDataOwnerProvider` decide what `CreatedBy`/`UpdatedBy` mean.** Skip `AddCurrentUserProvider` and those fields silently repurpose to the tenant ownership key instead of naming a person.
- **`AddPdfGenerator` takes a plain `PdfGeneratorOptions?` object, not an `Action<TOptions>` delegate** — the one package that breaks the four-shapes pattern in Rule 6.
- **The two Roslyn generators target `netstandard2.0`, not `net10.0`** — that lets the compiler load them; it does not change what your app targets.
- **`AddEventPublisher`, `AddSlimBusEventPublisher`, `AddSlimBusEfCoreInterceptor`, and `AddBackgroundJob` live in the ambient `Microsoft.Extensions.DependencyInjection` namespace** — no extra `using` needed, unlike most other DKNet setup calls. See `references/namespace-imports.md`.
- **`UseEndpointConfigs()` throws at startup if versioning is on without `AddApiVersioning()`.** Pass `configureOptions: o => o.EnableVersioning = false` if you are not ready to version endpoints yet.

## Do not

- `services.AddDKNet()` — does not exist. Nothing aggregates the packages; register each one you need.
- `AggregateRoot` — does not exist in any DKNet namespace. Use `Entity<TKey>`, `Entity`, `AuditedEntity<TKey>`, or `AuditedEntity`.
- `DKNet.EfCore.Repos`, `DKNet.EfCore.Repos.Abstractions` — removed, never published; `dotnet add package` 404s. Use `DKNet.EfCore.Specifications`.
- `IRepository<T>`, `IReadRepository<T>`, `IWriteRepository<T>`, `IRepositoryFactory`, `Repository<T>` — removed with the packages above. Use `IRepositorySpec`/`IRepositorySpecFactory`.
- `DKNet.EfCore.DtoEntities` — never a published package name; the current internal test fixture is `EfCore.DtoGenerator.TestEntities`, not something an application references.
- `IAesEncryption`, `AesEncryption`, `AddAesEncryption(...)` — deleted, no compatibility shim. Use `AddAesGcmEncryption(base64Key)`.
- `AddIdempotencyWithRedisStore`/`...MsSqlStore`/`...NpgsqlStore` are not in the base `DKNet.AspCore.Idempotency` namespace — each lives in its own store's namespace.
- `EncryptionKeyProvider` abstract class — gone; implement `IEncryptionKeyProvider` directly.
- `ToProblemDetails(...)` public overloads — gone; call `.Response()`/`.Response<T>()`.

The rest of the removed/renamed surface (typo'd names, changed parameter types, dropped interfaces) is in `references/removed-and-renamed.md` — check it before assuming an unfamiliar call is new rather than deleted.

```csharp
// no-compile
public sealed class ProductRepository : IRepository<Product> // IRepository<T> was removed
{
}
```

## Related skills

- `dknet-packages` — this skill: package router and setup order. Start here.
- `dknet-efcore-domain-model` — entities and EF Core model wiring (`DKNet.EfCore.Abstractions`, `DKNet.EfCore.Extensions`, `DKNet.EfCore.Relational.Helpers`).
- `dknet-efcore-specifications` — `IRepositorySpec` and dynamic predicates (`DKNet.EfCore.Specifications`).
- `dknet-efcore-save-pipeline` — `SaveChanges` hooks, domain events, audit logs (`DKNet.EfCore.Hooks`, `DKNet.EfCore.Events`, `DKNet.EfCore.AuditLogs`).
- `dknet-efcore-data-security` — row-level data authorization and column encryption (`DKNet.EfCore.DataAuthorization`, `DKNet.EfCore.Encryption`).
- `dknet-codegen` — source generators for DTOs and CRUD vertical slices (`DKNet.EfCore.DtoGenerator`, `DKNet.SlimBus.Generators`).
- `dknet-slimbus-cqrs` — CQRS with SlimMessageBus (`DKNet.SlimBus.Extensions`, `Aspire.Hosting.ServiceBus`).
- `dknet-aspcore-api` — minimal-API endpoint conventions and start-up tasks (`DKNet.AspCore.Extensions`, `DKNet.AspCore.Tasks`).
- `dknet-idempotency` — idempotent endpoints and key stores (`DKNet.AspCore.Idempotency` and its `.Relational`/`.MsSqlStore`/`.NpgsqlStore`/`.RedisStore` packages).
- `dknet-blob-storage` — blob storage abstraction and adapters (`DKNet.Svc.BlobStorage.Abstractions`, `.AwsS3`, `.AzureStorage`, `.Local`).
- `dknet-services` — application services: encryption, PDF, templates (`DKNet.Svc.Encryption`, `DKNet.Svc.PdfGenerators`, `DKNet.Svc.Transformation`).
- `dknet-core-utilities` — foundation helpers and secure random strings (`DKNet.Fw.Extensions`, `DKNet.RandomCreator`).
- `dknet-testing` — testing code built on DKNet, and testing inside the DKNet repo.

## References

- `references/package-catalog.md` — all 29 packages: id, install command, one-line purpose, entry point, owning skill.
- `references/removed-and-renamed.md` — every removed/renamed API across the suite, with its replacement and source-verification status.
- `references/namespace-imports.md` — the full method/type -> namespace table, including which setup calls are ambient to `Microsoft.Extensions.DependencyInjection` and need no extra `using`.
- Docs hub: https://github.com/baoduy/DKNet/blob/dev/docs/README.md
- Getting started: https://github.com/baoduy/DKNet/blob/dev/docs/Getting-Started.md
- Architecture guide: https://github.com/baoduy/DKNet/blob/dev/docs/Architecture.md
- Full API/docs site: https://baoduy.github.io/DKNet/
