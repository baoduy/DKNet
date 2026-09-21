# Namespace and import table

Missing-`using` errors, not missing-package errors: the package restores fine, but the call or type
does not resolve until the right namespace is imported. A few DKNet setup methods are deliberately
declared in an *ambient* namespace — one owned by ASP.NET Core or `Microsoft.Extensions.*` rather
than by DKNet — specifically so they resolve with no extra `using` at all, the same way EF Core's own
`AddDbContext` does. This file says, for each method or type, exactly which namespace it needs.

An agent writing a `Program.cs`-style file already gets `Microsoft.AspNetCore.Builder`,
`Microsoft.AspNetCore.Http`, `Microsoft.AspNetCore.Routing`, `Microsoft.Extensions.DependencyInjection`,
`Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Configuration`,
`System`, `System.Linq`, `System.Threading.Tasks`, and `System.Collections.Generic` from the SDK's
implicit usings. Every row marked **(ambient)** below sits inside one of those namespaces, not a
`DKNet.*` one — so it needs nothing beyond what a bare ASP.NET Core project already has.

| Method / type | Namespace | Notes |
|---|---|---|
| `AddDbContext<TContext>()` | `Microsoft.Extensions.DependencyInjection` (ambient — EF Core's own extension) | Plain EF Core registration; no hook interceptor. |
| `UseAutoConfigModel<TContext>()`, `UseAutoConfigModel(Assembly[])` | `Microsoft.EntityFrameworkCore` — not implicit for a bare ASP.NET Core project; add `using Microsoft.EntityFrameworkCore;` | The `<TContext>` overload needs a `DbContextOptionsBuilder<TContext>` receiver; the `Assembly[]` overload takes the plain, non-generic `DbContextOptionsBuilder` that `AddDbContext`'s own callback hands you. Call either inside the `AddDbContext(WithHook)` options callback, never in `OnModelCreating`. |
| `AddDbContextWithHook<TDbContext>()`, `AddHook<TDbContext,THook>()`, `UseHooks<TDbContext>()`, `DisableHooks()` | `DKNet.EfCore.Hooks` | |
| `AddSpecRepo<TDbContext>()` | `DKNet.EfCore.Specifications` | |
| `IRepositorySpec`, `IRepositorySpecFactory`, `RepositorySpec<TDbContext>` | `DKNet.EfCore.Specifications.Repositories` | |
| `Specification<TEntity>`, `ISpecification<TEntity>` | `DKNet.EfCore.Specifications.Definitions` | Not `DKNet.EfCore.Specifications`, which holds only `AddSpecRepo`. |
| `ToListAsync(spec, ct)`, `FirstAsync`, `FirstOrDefaultAsync`, `ToPagedListAsync`, `ToPageEnumerable`, `ToKeysetPageAsync`, `AnyAsync`, `CountAsync` (all extend `IRepositorySpec`) | `DKNet.EfCore.Specifications.Extensions` | |
| `Ops` | `DKNet.EfCore.Specifications.Dynamics` | The operation enum `DynamicAnd`/`DynamicOr`/`TryBuildPredicate` take as their second argument. |
| `DynamicAnd`, `DynamicOr`, `TryBuildPredicate` | `LinqKit` (ambient — the file is declared `namespace LinqKit;`, extending `ExpressionStarter<T>`/`Expression<Func<T,bool>>`) | No `using DKNet.EfCore.Specifications.Dynamics;` needed for these three specifically — only the `using LinqKit;` that `PredicateBuilder` itself already requires, the same "ambient namespace" convention as the DI extension methods below. |
| `Entity<TKey>`, `Entity`, `AuditedEntity<TKey>`, `AuditedEntity`, `IEntity<TKey>`, `IAuditedProperties` | `DKNet.EfCore.Abstractions.Entities` | |
| `IEventPublisher`, `DefaultEventPublisher` | `DKNet.EfCore.Abstractions.Events` | |
| `AddEventPublisher<TDbContext,TImplementation>()` | `Microsoft.Extensions.DependencyInjection` (ambient) | Needs no `using DKNet.EfCore.Events;` at all. |
| `EventException` | `DKNet.EfCore.Events` | |
| `AddEfCoreAuditLogs<TDbContext,TPublisher>()`, `AddCurrentUserProvider<TDbContext,TProvider>()`, `ICurrentUserProvider`, `IAuditLogPublisher` | `DKNet.EfCore.AuditLogs` | |
| `AddDataOwnerProvider<TDbContext,TProvider>()`, `IDataOwnerDbContext`, `IOwnedBy`, `IDataOwnerProvider` | `DKNet.EfCore.DataAuthorization` | |
| `AddEfCoreEncryption<TKeyServiceImplementation>()`, `IEncryptionKeyProvider` | `DKNet.EfCore.Encryption` | Generic parameter is `TKeyServiceImplementation`, not `TKeyProvider`. |
| `AddSlimBusEventPublisher<TDbContext>()`, `AddSlimBusEfCoreInterceptor<TDbContext>()` | `Microsoft.Extensions.DependencyInjection` (ambient) | Needs no `using DKNet.SlimBus.Extensions;` at all. |
| `AddSlimMessageBus(...)`, `.AddServicesFromAssembly(...)`, `.AddChildBus(...)` | `SlimMessageBus.Host` | Plain SlimMessageBus API, not a DKNet namespace. |
| `.WithProviderMemory()`, `.AutoDeclareFrom(...)` | `SlimMessageBus.Host.Memory` | Only needed with the in-memory transport. |
| `.AddJsonSerializer()` | `SlimMessageBus.Host.Serialization.SystemTextJson` | |
| `AddIdempotentKey(...)`, `AddIdempotentKey<TSoreImplement>(...)`, `.RequiredIdempotentKey()` | `DKNet.AspCore.Idempotency` | The generic parameter is spelled `TSoreImplement` in source — a typo, not `TStoreImplement`. |
| `AddIdempotencyMsSqlStore(...)`, `AddIdempotencyWithMsSqlStore(...)` | `DKNet.AspCore.Idempotency.MsSqlStore` | |
| `AddIdempotencyNpgsqlStore(...)`, `AddIdempotencyWithNpgsqlStore(...)` | `DKNet.AspCore.Idempotency.NpgsqlStore` | |
| `AddIdempotencyRedisStore(...)`, `AddIdempotencyWithRedisStore(...)` | `DKNet.AspCore.Idempotency.RedisStore` | |
| `AddBackgroundJob<TJob>()`, `AddBackgroundJobFrom(Assembly[])` | `Microsoft.Extensions.DependencyInjection` (ambient) | |
| `IBackgroundTask` | `DKNet.AspCore.Tasks` | |
| `UseEndpointConfigs(...)`, `EndpointRegistrationOptions` | `DKNet.AspCore.Extensions.Endpoints` | Throws at startup if `EnableVersioning` (default `true`) is left on without `AddApiVersioning()`. |
| `IEndpointConfig` | `DKNet.AspCore.Extensions` | |
| `.Response(...)`, `.Response<T>(...)` | `DKNet.AspCore.Extensions.Responses` | |
| `AddContextualRequestPopulation()`, `IContextualSource`, `FromClaimAttribute` | `DKNet.AspCore.Extensions.ModelBinding` | |
| `AddAesGcmEncryption(base64Key)`, `AddRsaEncryption(privateKeyBase64)`, `AddEncryptionServices()` | `DKNet.Svc.Encryption` | `AddEncryptionServices()` registers only `IShaHashing`/`IHmacHashing` — it is not a cipher. |
| `AddPdfGenerator(options)` | `DKNet.Svc.PdfGenerators` | Takes a `PdfGeneratorOptions?` object, not an `Action<TOptions>` delegate. |
| `AddTransformerService()` | `DKNet.Svc.Transformation` | |
| `AddS3BlobService(...)` | `DKNet.Svc.BlobStorage.AwsS3` | |
| `AddAzureStorageAdapter(...)` | `DKNet.Svc.BlobStorage.AzureStorage` | |
| `AddLocalDirectoryBlobService(...)` | `DKNet.Svc.BlobStorage.Local` | |
| `IBlobService` | `DKNet.Svc.BlobStorage.Abstractions` | |

## Why the ambient ones matter

`AddEventPublisher`, `AddSlimBusEventPublisher`, `AddSlimBusEfCoreInterceptor`, and `AddBackgroundJob`
are declared with `// ReSharper disable once CheckNamespace` directly inside
`namespace Microsoft.Extensions.DependencyInjection;` in their DKNet source files — the same pattern
`AddDbContext` itself uses. Adding a `using DKNet.EfCore.Events;`/`using DKNet.SlimBus.Extensions;`
for these specific calls is harmless but unnecessary; do not assume every DKNet setup method needs
its own package namespace imported the way `AddSpecRepo`/`AddDbContextWithHook`/`AddDataOwnerProvider`
do.

## Related

- `references/package-catalog.md` — the package each namespace above ships from.
- `SKILL.md`'s Rules and Gotchas sections for the setup-order consequences of getting these wrong.
