# Frequently Asked Questions

## Contents

- [General](#general)
- [Architecture & design](#architecture--design)
- [Entity Framework Core](#entity-framework-core)
- [CQRS & messaging](#cqrs--messaging)
- [Performance](#performance)
- [Testing](#testing)
- [Deployment](#deployment)
- [Troubleshooting](#troubleshooting)

---

## General

### What is DKNet?

A suite of 28 independent .NET NuGet packages for building enterprise applications around Domain-Driven Design
and Onion Architecture. There is no framework to adopt wholesale: each package registers itself and is usable on
its own. Start from [Which package do I need?](README.md#which-package-do-i-need).

### Which .NET versions are supported?

.NET 10.0. `src/Directory.Packages.props` sets `net10.0` solution-wide, so every shipped package targets it —
the two exceptions are the Roslyn source generators, `DKNet.EfCore.DtoGenerator` and `DKNet.SlimBus.Generators`,
which must target `netstandard2.0` to load into the compiler. They still generate code for your `net10.0` project.
`src/global.json` pins SDK `10.0.0` with `rollForward: latestMajor`.

### Is it free to use?

Yes — [MIT licensed](https://github.com/baoduy/DKNet/blob/main/LICENSE), for commercial and non-commercial use.

### How is quality enforced?

`src/Directory.Build.props` sets `TreatWarningsAsErrors`, `Nullable=enable` and `GenerateDocumentationFile`
solution-wide, so a new warning, a missing XML doc comment, or a nullable mismatch breaks the build. Every
package has a sibling `*.Tests` project, and CI fails below 80% line coverage. The stricter per-area targets are
in [Testing Strategy](Testing-Strategy.md) — they are targets, not a measured guarantee.

### Where can I get help?

- **Issues**: [GitHub Issues](https://github.com/baoduy/DKNet/issues)
- **Discussions**: [GitHub Discussions](https://github.com/baoduy/DKNet/discussions)
- **Examples**: [Examples & Recipes](Examples/README.md), or the SlimBus.ApiEndpoints template in the [DKNet.Templates](https://github.com/baoduy/DKNet.Templates) repository

---

## Architecture & design

### Do I have to use DDD?

No. Most packages have nothing to do with DDD — `DKNet.Fw.Extensions`, `DKNet.RandomCreator`, the blob storage
family, `DKNet.Svc.PdfGenerators`, `DKNet.Svc.Transformation`, `DKNet.AspCore.Tasks`, and
`DKNet.AspCore.Idempotency` all work in any architecture. The DDD-shaped ones are `DKNet.EfCore.Abstractions`
(entities with a domain-event queue) and the packages that build on it.

### Do I need every EF Core package?

No. A useful minimum is `DKNet.EfCore.Abstractions` plus `DKNet.EfCore.Extensions`; add the rest as the need
appears:

- **Querying and persistence** → `DKNet.EfCore.Specifications`
- **Domain events** → `DKNet.EfCore.Events` (which needs `DKNet.EfCore.Hooks` wiring via `AddDbContextWithHook`)
- **Change history** → `DKNet.EfCore.AuditLogs`
- **Row-level isolation** → `DKNet.EfCore.DataAuthorization`

`DKNet.EfCore.Repos` and `DKNet.EfCore.Repos.Abstractions` are retired and never published to NuGet. Use
`DKNet.EfCore.Specifications`; the call-site mapping is in
[Migrating-Repos-To-Specifications](EfCore/Migrating-Repos-To-Specifications.md).

### Why is there no `AddDKNet()`?

Because there is nothing for it to do. No package needs another to be registered first (beyond the ordering
listed in [Registration order that matters](Configuration.md#registration-order-that-matters)), and an aggregator
would force references you do not want. See [Configuration & Setup](Configuration.md).

### Where do I read the whole story end to end?

[Architecture Guide](Architecture.md) — it carries the layer map, the package dependency graph, a request
lifecycle across packages, and the domain-event path from an entity method to a bus consumer.

---

## Entity Framework Core

### How do I handle migrations?

Standard EF Core tooling — DKNet adds nothing:

```bash
dotnet ef migrations add YourMigrationName
dotnet ef database update

# For a deployment pipeline
dotnet ef migrations script --idempotent --output migration.sql
```

### Can I use multiple DbContexts?

Yes, and each is configured independently:

```csharp
using DKNet.EfCore.Specifications;
using Microsoft.EntityFrameworkCore;

services.AddDbContext<CatalogContext>(options => options.UseSqlServer(catalogConnectionString));
services.AddDbContext<IdentityContext>(options => options.UseSqlServer(identityConnectionString));

services.AddSpecRepo<CatalogContext>();
services.AddSpecRepo<IdentityContext>();
```

One caveat: `AddDataOwnerProvider` registers its query filter in a **static** model-builder list, so it applies to
every `DbContext` that calls `UseAutoConfigModel()` — not only the one passed as `TDbContext`. A second context
whose model contains `IOwnedBy` entities must also implement `IDataOwnerDbContext`, or keep those entities out of
its model.

### How do I handle multi-tenancy?

Via `DKNet.EfCore.DataAuthorization`:

1. Implement `IOwnedBy` on the entities that belong to a tenant.
2. Implement `IDataOwnerDbContext` on the `DbContext` — required, and enforced by the generic constraint on
   `AddDataOwnerProvider<TDbContext, TProvider>()`.
3. Register an `IDataOwnerProvider` that returns the current owner key.
4. Build the model through `UseAutoConfigModel<TContext>()`, which is what attaches the filter.

A global query filter then scopes every read, and a `SaveChanges` hook stamps the owner on new rows. Worked
example: [Multi-tenant application](Examples/README.md#multi-tenant-application).

### When are domain events dispatched?

In the **after-save** hook, so only once the database write has succeeded. Collection happens in the before-save
hook (that is the only point where `[RaisesEvent]` property narrowing can read `IsModified`). A failed save
publishes nothing. Details: [A domain event end to end](Architecture.md#a-domain-event-end-to-end).

### Why did my domain event not fire?

Four common causes, in order of likelihood:

1. **The `DbContext` was registered with `AddDbContext`, not `AddDbContextWithHook`** — no hook interceptor, so no
   dispatch.
2. **No publisher is registered.** Use `AddEventPublisher<TDbContext, TImplementation>()`, or
   `AddSlimBusEventPublisher<TDbContext>()` to forward events onto SlimMessageBus.
3. **The save failed.** Events are published after the write commits, not before.
4. **A publisher threw.** Publisher failures are logged and swallowed, so the save succeeds and the event is
   lost — check the logs for the publisher's type name.

`AddEvent<TEvent>()` and `[RaisesEvent]` additionally require an `IMapper` registration; without one the save
throws `EventException` rather than dropping the event silently.

---

## CQRS & messaging

### Do I need MediatR?

No. `DKNet.SlimBus.Extensions` is built on [SlimMessageBus](https://github.com/zarusz/SlimMessageBus) and does not
reference MediatR. Nothing stops you using MediatR elsewhere in your own application, but DKNet ships no MediatR
integration. Handler discovery is SlimMessageBus's own API:

```csharp
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

services.AddSlimMessageBus(mbb => mbb
    .AddJsonSerializer()
    .AddServicesFromAssembly(typeof(Program).Assembly)   // discovers your Fluents handlers
    .AddChildBus("Memory", bus => bus
        .WithProviderMemory()
        .AutoDeclareFrom(typeof(Program).Assembly)));
```

### How do I validate a command?

Bring your own. DKNet ships **no validation pipeline** — declaring a FluentValidation `AbstractValidator<T>` next
to a command does nothing on its own. To run validation before a handler, register a SlimMessageBus
`IRequestHandlerInterceptor<TRequest, TResponse>` that resolves your validators and short-circuits with a failed
`Result`.

### Why does my handler not need SaveChangesAsync?

`AddSlimBusEfCoreInterceptor<TDbContext>()` registers an interceptor with `Order = int.MaxValue`, so it wraps the
handler and runs last. After the handler returns, it saves — but only when the response is non-null, is not a
failed `IResultBase`, and the request is a `Fluents.Requests` write. A failed command therefore leaves nothing
behind and needs no rollback code.

### Can event consumers be async?

Yes — `Fluents.EventsConsumers.IHandler<TEvent>.OnHandle` returns a `Task`:

```csharp
using DKNet.SlimBus.Extensions;

public class ProductCreatedHandler(IEmailService emailService)
    : Fluents.EventsConsumers.IHandler<ProductCreatedEvent>
{
    public Task OnHandle(ProductCreatedEvent message, CancellationToken cancellationToken) =>
        emailService.SendNotificationAsync($"Product {message.ProductId} created");
}
```

---

## Performance

### What does DKNet cost at runtime?

No published benchmarks exist, so treat any figure you see as unmeasured. What can be stated from the code:

- The hook pipeline is **one** EF Core interceptor per `DbContext` type, shared by Events, AuditLogs, and
  DataAuthorization — not one interceptor each.
- `DKNet.EfCore.DtoGenerator` and `DKNet.SlimBus.Generators` do their work at compile time and add no runtime
  reflection.
- `[RaisesEvent]` attribute lookups are cached per entity `Type` for the lifetime of the process.
- `DKNet.EfCore.Specifications` composes a normal `IQueryable`, so EF Core's own query plan caching applies.

Anything beyond that should be measured in your own application.

### How do I keep queries cheap?

Real APIs the packages give you, rather than general advice:

1. **Project instead of materialising entities.** `repo.ToListAsync<TEntity, TModel>(spec, ct)` and
   `repo.FirstOrDefaultAsync<TEntity, TModel>(spec, ct)` project through Mapster — they need an `IMapper`
   registration, and they apply `AsNoTracking()` for you.
2. **Use keyset pagination for deep pages.** `repo.ToKeysetPageAsync(spec, keySelector, cursor, pageSize, ct)`
   generates an index seek instead of the growing `OFFSET` that `ToPagedListAsync` produces.
3. **Delete in the database.** `repo.BulkDeleteAsync<TEntity>(predicate, ct)` issues `ExecuteDeleteAsync` rather
   than loading and tracking rows.
4. **Stream large result sets.** `repo.ToPageEnumerable(spec)` returns an `IAsyncEnumerable<TEntity>` and requires
   the specification to declare an ordering.
5. **Check the SQL.** `repo.Query(spec).ToQueryString()` shows what the specification actually translated to —
   the pattern used throughout the test suite.

---

## Testing

### What does DKNet's own test suite use?

xUnit, Shouldly, and TestContainers.MsSql for integration tests — no mocked `DbContext`. See
[Testing Strategy](Testing-Strategy.md) for conventions and coverage targets.

### TestContainers or the in-memory provider?

**TestContainers** for anything that exercises persistence. The EF Core in-memory provider does not translate
global query filters, generated SQL, or sequences, so it silently passes tests that would fail against a real
database — which is exactly the behaviour several DKNet packages depend on.

**In-memory or no database at all** for pure domain logic: entity invariants, the domain-event queue, and
specification construction all test fine without a provider.

### How do I test that a domain method raised an event?

`IEventEntity.GetEvents()` returns the queued event instances and the queued event types as a tuple, so no DKNet
test helper is needed:

```csharp
using Shouldly;
using Xunit;

[Fact]
public void UpdatePrice_RaisesPriceChangedEvent()
{
    var product = Product.Create("Widget", 10.0m, "user");

    product.UpdatePrice(15.0m, "user");

    var (events, _) = product.GetEvents();
    events.ShouldHaveSingleItem().ShouldBeOfType<ProductPriceChangedEvent>();
}
```

### How do I test ownership filtering?

Register a fake `IDataOwnerProvider` and let the filter do its job — it is attached at model-build time and cannot
be disabled per query:

```csharp
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

[Fact]
public async Task Query_OnlyReturnsRowsForTheCurrentOwner()
{
    // The provider is what decides the current owner; the filter is already on the model.
    var invoices = await context.Set<Invoice>().ToListAsync();

    invoices.ShouldAllBe(i => i.OwnedBy == "tenant1");
}
```

Keep `UseAutoConfigModel<TContext>()` in the test host's registration. Dropping it removes the filter, and the
test then passes for the wrong reason.

---

## Deployment

### How do I deploy an application that uses DKNet?

Like any .NET application — DKNet adds no deployment requirement. Two things are worth checking:

- **`DKNet.Svc.PdfGenerators` needs a Chromium browser** (PuppeteerSharp downloads or locates one). A slim
  container image may not have its dependencies.
- **The relational idempotency stores need their table.** `DKNet.AspCore.Idempotency.Relational` ships a
  `DbContext` for it; create it with a migration or with
  `DKNet.EfCore.Relational.Helpers`' `CreateTableAsync<TEntity>()`.

### Migrations in production?

Generate an idempotent script (`dotnet ef migrations script --idempotent`) or a migration bundle and run it as a
deployment step, rather than migrating from application start-up.

### Where do secrets go?

Standard .NET configuration providers — Azure Key Vault, AWS Secrets Manager, environment variables, user secrets
for local work. The DKNet-specific values are the blob adapter connection strings, the idempotency store
connection string, and the AES/RSA key material. See
[Environment and secrets](Configuration.md#environment-and-secrets).

---

## Troubleshooting

### "does not contain a definition for `AddDbContextWithHook`" (or another `Add*`)

A missing `using`, not a missing package. Several DKNet extensions live in their own namespace rather than
`Microsoft.Extensions.DependencyInjection` — the full map is in
[Where each extension method lives](Configuration.md#where-each-extension-method-lives). The ones that catch
people most often:

| Method | Namespace |
|---|---|
| `AddDbContextWithHook<T>()` | `DKNet.EfCore.Hooks` |
| `AddSpecRepo<T>()` | `DKNet.EfCore.Specifications` |
| `ToListAsync(spec, ct)` and friends | `DKNet.EfCore.Specifications.Extensions` |
| `AddIdempotencyWithRedisStore(...)` | `DKNet.AspCore.Idempotency.RedisStore` |
| `.Response(...)` | `DKNet.AspCore.Extensions.Responses` |

### "`ModelBuilder` does not contain a definition for `UseAutoConfigModel`"

It is a `DbContextOptionsBuilder<TContext>` extension, not a `ModelBuilder` one. It goes inside the
`AddDbContext`/`AddDbContextWithHook` callback:

```csharp
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;

services.AddDbContextWithHook<AppDbContext>(options => options
    .UseSqlServer(connectionString)
    .UseAutoConfigModel<AppDbContext>());
```

### CS0311 on `AddDataOwnerProvider<TDbContext, TProvider>()`

Your `DbContext` does not implement `IDataOwnerDbContext`. The constraint is deliberate — the older, unconstrained
signature let you register a context the ownership filter could not read, which silently disabled row isolation.
Fix and background: [Migration Guide](Migration-Guide.md#upgrading-dknetefcoredataauthorization-idataownerdbcontext-is-now-required).

### "Unable to resolve service for type `IRepositorySpec`"

`AddSpecRepo<TDbContext>()` was not called, or was called for a different `DbContext` type than the one registered.

### A dynamic predicate returns everything, or throws at query time

Two separate causes:

- **Running it outside `IRepositorySpec`.** `RepositorySpec<TDbContext>.Query<TEntity>` calls `.AsExpandable()`
  for you; a predicate you build and execute against a raw `DbSet` needs that call by hand or LinqKit cannot
  expand it.
- **Wrapping calls in null checks.** `DynamicAnd`/`DynamicOr` already handle a null or unusable value by skipping
  the clause rather than throwing, so an outer `if (value != null)` only hides which clause was dropped.

### An idempotent endpoint is not deduplicating

Two causes:

- **No store registered.** `.RequiredIdempotentKey()` always adds the filter, but the filter needs an
  `IIdempotencyKeyStore` from `AddIdempotentKey`/`AddIdempotencyWith*Store`; without it the route fails on its
  first request rather than running unprotected.
- **A second `AddIdempotentKey` call was ignored.** It returns early when a store is already registered, so a
  later call with different `IdempotencyOptions` has no effect. Register the store once, with the options you
  want.

If deduplication works but two callers collide or fail to, check the caller scope — see
[Security](Security.md#idempotency-keys).

---

## More

- **[Getting Started](Getting-Started.md)** — prerequisites and a first working setup
- **[Configuration & Setup](Configuration.md)** — registration conventions and ordering
- **[Examples & Recipes](Examples/README.md)** — runnable implementations
- **[Architecture Guide](Architecture.md)** — the composition story
- **[API Reference](API-Reference.md)** — per-package index

Still stuck? [Open an issue](https://github.com/baoduy/DKNet/issues) or start a
[discussion](https://github.com/baoduy/DKNet/discussions).
