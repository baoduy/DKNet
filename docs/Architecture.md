# Architecture Guide

This guide explains the architectural principles behind the DKNet Framework, and how they map onto its actual
packages and types — every type and API name below is real and grep-verified against `src/`, not illustrative.

## Table of Contents

- [Architectural Overview](#architectural-overview)
- [Which package owns which concern](#which-package-owns-which-concern)
- [Package dependencies](#package-dependencies)
- [Domain-Driven Design (DDD)](#domain-driven-design-ddd)
- [Onion Architecture](#onion-architecture)
- [CQRS via DKNet.SlimBus.Extensions](#cqrs-via-dknetslimbusextensions)
- [A request end to end](#a-request-end-to-end)
- [A domain event end to end](#a-domain-event-end-to-end)
- [Specification Pattern](#specification-pattern)
- [Cross-Cutting Concerns](#cross-cutting-concerns)

---

## Architectural Overview

DKNet Framework is a suite of independent .NET NuGet packages built around **Domain-Driven Design (DDD)** and the
**Onion Architecture** pattern. Rather than a single application skeleton, DKNet expresses these patterns at
**package boundaries**: each ring of the onion is a separate, opt-in package, so a consuming application pulls in
only what it needs and can swap an implementation (a blob provider, an idempotency store) without touching domain
code.

![The DKNet onion: the presentation ring holds the DKNet.AspCore packages, the application ring holds SlimBus.Extensions and the Svc adapters, the infrastructure ring holds the EF Core packages, and DKNet.EfCore.Abstractions plus the dependency-free foundation packages sit at the centre. Every arrow is a project reference pointing inward.](./diagrams/dknet-layers.svg)

Three packages sit deliberately outside those rings:

- `DKNet.EfCore.Encryption` attaches to the model as an EF Core `ValueConverter`, not through the hook pipeline.
- `DKNet.EfCore.DtoGenerator` and `DKNet.SlimBus.Generators` are Roslyn generators: they run at compile time and
  ship no runtime service.
- `Aspire.Hosting.ServiceBus` configures an Aspire AppHost, not the application under it.

---

## Which package owns which concern

Each row is one concern and the single package responsible for it. Nothing here is shared between two packages,
and no package exists only to aggregate others.

| Concern | Owner | Attaches via |
|---|---|---|
| Entity identity, audit fields, domain-event queue | [`DKNet.EfCore.Abstractions`](./EfCore/DKNet.EfCore.Abstractions.md) | base classes you derive from |
| Model build: configuration discovery, global filters, seeding, GUID v7 keys, sequences | [`DKNet.EfCore.Extensions`](./EfCore/DKNet.EfCore.Extensions.md) | `UseAutoConfigModel<TContext>()` on `DbContextOptionsBuilder` |
| Querying and persistence | [`DKNet.EfCore.Specifications`](./EfCore/DKNet.EfCore.Specifications.md) | `AddSpecRepo<TDbContext>()` |
| Running code around `SaveChanges` | [`DKNet.EfCore.Hooks`](./EfCore/DKNet.EfCore.Hooks.md) | `AddDbContextWithHook<TDbContext>()` |
| Dispatching domain events | [`DKNet.EfCore.Events`](./EfCore/DKNet.EfCore.Events.md) | a hook, registered by `AddEventPublisher<TDbContext, TImpl>()` |
| Field-level change history | [`DKNet.EfCore.AuditLogs`](./EfCore/DKNet.EfCore.AuditLogs.md) | a hook, registered by `AddEfCoreAuditLogs<TDbContext, TPublisher>()` |
| Row-level ownership | [`DKNet.EfCore.DataAuthorization`](./EfCore/DKNet.EfCore.DataAuthorization.md) | a global query filter plus a hook, registered by `AddDataOwnerProvider<TDbContext, TProvider>()` |
| Column encryption | [`DKNet.EfCore.Encryption`](./EfCore/DKNet.EfCore.Encryption.md) | a `ValueConverter` on the model |
| DTO shapes | [`DKNet.EfCore.DtoGenerator`](./EfCore/DKNet.EfCore.DtoGenerator.md) | compile time only |
| Command/query dispatch and automatic save | [`DKNet.SlimBus.Extensions`](./Messaging/DKNet.SlimBus.Extensions.md) | `AddSlimBusEfCoreInterceptor<TDbContext>()` |
| Forwarding domain events onto the bus | [`DKNet.SlimBus.Extensions`](./Messaging/DKNet.SlimBus.Extensions.md) | `AddSlimBusEventPublisher<TDbContext>()` |
| HTTP surface: endpoint groups, model binding, result mapping | [`DKNet.AspCore.Extensions`](./AspNetCore/DKNet.AspCore.Extensions.md) | `UseEndpointConfigs()` and `.Response()` |
| Retry safety for a write endpoint | [`DKNet.AspCore.Idempotency`](./AspNetCore/DKNet.AspCore.Idempotency.md) plus one store | `.RequiredIdempotentKey()` on the route |
| Start-up work | [`DKNet.AspCore.Tasks`](./AspNetCore/DKNet.AspCore.Tasks.md) | `AddBackgroundJob<TJob>()` |
| Files, PDFs, template tokens, standalone cryptography | [`DKNet.Svc.*`](./Services/README.md) | its own `Add*` method; none of them touch EF Core |
| Type scanning and framework-agnostic helpers | [`DKNet.Fw.Extensions`](./Core/DKNet.Fw.Extensions.md) | plain static methods, no registration |

There is no `DKNetOptions` and no single aggregator — see [Configuration & Setup](Configuration.md) for the four
registration conventions these methods fall into.

---

## Package dependencies

![Package dependency map of the DKNet onion: presentation, application and infrastructure packages all depend inward toward DKNet.EfCore.Abstractions, with Events, AuditLogs and DataAuthorization attaching through DKNet.EfCore.Hooks.](./diagrams/dknet-onion-packages.svg)

Every arrow is a real project reference in `src/`: dependencies only ever point inward, toward
`DKNet.EfCore.Abstractions`. `DKNet.Svc.*` (blob storage, encryption, PDF, transformation) sits in the
application ring alongside `DKNet.SlimBus.Extensions` but has no dependency on the EF Core rings at all, which is
why it carries no arrow here. `DKNet.EfCore.Encryption` is likewise absent: it attaches to the `DbContext` as a
value converter rather than through the hook pipeline.

---

## Domain-Driven Design (DDD)

DDD focuses modeling on the core business domain. DKNet supports this with rich, event-raising entities rather than
anemic data bags — see [`DKNet.EfCore.Abstractions`](./EfCore/DKNet.EfCore.Abstractions.md) for the full contract
surface.

### Ubiquitous language and rich behavior

State changes through intention-revealing methods that enforce invariants and raise events, not public setters:

```csharp
using DKNet.EfCore.Abstractions.Entities;

public class Product : AuditedEntity // Guid-keyed, audit-tracked, event-capable
{
    private Product() { }

    public static Product Create(string name, decimal price, string createdBy)
    {
        var product = new Product { Name = name, Price = price, IsActive = true };
        product.SetCreatedBy(createdBy);
        product.AddEvent(new ProductCreatedEvent(product.Id));
        return product;
    }

    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; }

    public void UpdatePrice(decimal newPrice, string userId)
    {
        if (!IsActive) throw new InvalidOperationException("Cannot update price for an inactive product");

        Price = newPrice;
        SetUpdatedBy(userId);
        AddEvent(new ProductPriceChangedEvent(Id, newPrice));
    }
}

public record ProductCreatedEvent(Guid ProductId);
public record ProductPriceChangedEvent(Guid ProductId, decimal NewPrice);
```

There is no separate `AggregateRoot` type shipped by DKNet — `Entity` / `AuditedEntity` (both in
`DKNet.EfCore.Abstractions`) already carry the domain-event queue (`IEventEntity.AddEvent`) and, for
`AuditedEntity`, creation/modification tracking. A consistency boundary in DKNet is simply an entity you choose to
treat as one — model the entity graph so that only the root is fetched and saved directly by application code.

### Bounded contexts

Within a solution, models are organized into bounded contexts, each with its own `DbContext` and entity set.
Persistence is reached only through [`DKNet.EfCore.Specifications`](./EfCore/DKNet.EfCore.Specifications.md)'s
`IRepositorySpec`, so domain code never depends on EF Core directly, and application orchestration runs through
[`DKNet.SlimBus.Extensions`](./Messaging/DKNet.SlimBus.Extensions.md) handlers that load entities, invoke their
behavior, and let the auto-save interceptor persist the result.

---

## Onion Architecture

Dependencies always flow inward toward the domain. DKNet expresses this at the package level, not via a single
application project:

1. **Domain layer (core)** — entities, value objects, and domain logic with no infrastructure dependencies. Anchored
   by [`DKNet.EfCore.Abstractions`](./EfCore/DKNet.EfCore.Abstractions.md) (`Entity`, `AuditedEntity`, `IEventEntity`).
2. **Application layer** — orchestrates the domain through command/query handlers. Realized by
   [`DKNet.SlimBus.Extensions`](./Messaging/DKNet.SlimBus.Extensions.md) and the `DKNet.Svc.*` service adapters.
3. **Infrastructure layer** — implements interfaces declared by inner layers:
   [`DKNet.EfCore.Specifications`](./EfCore/DKNet.EfCore.Specifications.md) for persistence, plus the SaveChanges
   interceptor pipeline ([`DKNet.EfCore.Hooks`](./EfCore/DKNet.EfCore.Hooks.md),
   [`DKNet.EfCore.Events`](./EfCore/DKNet.EfCore.Events.md),
   [`DKNet.EfCore.AuditLogs`](./EfCore/DKNet.EfCore.AuditLogs.md),
   [`DKNet.EfCore.DataAuthorization`](./EfCore/DKNet.EfCore.DataAuthorization.md)).
4. **Presentation layer** — [`DKNet.AspCore.*`](./AspNetCore/README.md) minimal-API endpoints that depend only on
   the application layer.

Because DKNet is a library suite, each ring is a separate NuGet package: a consuming application swaps an
infrastructure adapter (a different blob provider, a different idempotency store) without touching domain or
application code.

---

## CQRS via DKNet.SlimBus.Extensions

[`DKNet.SlimBus.Extensions`](./Messaging/DKNet.SlimBus.Extensions.md) is DKNet's CQRS pipeline, built on
[SlimMessageBus](https://github.com/zarusz/SlimMessageBus) rather than MediatR — it is lighter (no reflection-heavy
pipeline behaviors) and result-based (`FluentResults`) for expected business failures. It also auto-saves the
`DbContext` after a successful write, so handlers don't call `SaveChangesAsync` themselves.

```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;

public record DeactivateProduct(Guid ProductId) : Fluents.Requests.INoResponse;

internal sealed class DeactivateProductHandler(AppDbContext db)
    : Fluents.Requests.IHandler<DeactivateProduct>
{
    public async Task<IResultBase> OnHandle(DeactivateProduct request, CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync([request.ProductId], cancellationToken);
        if (product is null) return Result.Fail("Product not found");

        product.Deactivate();
        // No SaveChangesAsync call here — the auto-save interceptor does it after this returns Ok.
        return Result.Ok();
    }
}
```

Registration wires two things: `AddSlimBusEfCoreInterceptor<TDbContext>()` for auto-save, and plain SlimMessageBus
setup (`AddSlimMessageBus`, a transport provider) for dispatch. See the package page for the full fluent interface
surface (`INoResponse`, `IHandler<TRequest>`, request/response variants) and query-side handlers.

---

## A request end to end

Nothing in DKNet chains the packages together for you — each one attaches at a different extension point. This is
what a retry-safe write request touches, in order, when all of them are wired up:

![Sequence diagram of one HTTP write request: the client posts with an idempotency key, the endpoint filter checks the key store, the route handler sends the request onto the bus, the handler mutates entities, the auto-save interceptor saves and the hooks publish events, and the filter caches the response before replying.](./diagrams/dknet-request-lifecycle.svg)

Three properties follow from that ordering, and they are the reason the packages compose rather than collide:

- **The idempotency filter runs outside everything else.** It is an `IEndpointFilter`, so a key that has already
  been processed replays the cached response without the bus, the handler, or the `DbContext` ever being reached.
- **The handler owns no persistence decision.** It mutates tracked entities and returns an `IResult<T>`; the
  auto-save interceptor calls `SaveChangesAsync` only for a successful `Fluents.Requests` write, so a failed
  command leaves nothing behind and needs no rollback code.
- **Hooks and events run inside that save.** By the time the handler's result reaches the endpoint, the audit rows
  are written and the domain events are published — there is no second phase to coordinate.

For the SlimBus interceptor chain in isolation (including where your own `IRequestHandlerInterceptor`
implementations sit relative to auto-save), see the sequence diagram on the
[`DKNet.SlimBus.Extensions`](./Messaging/DKNet.SlimBus.Extensions.md) page.

---

## A domain event end to end

Domain events are the one DKNet feature that spans four packages, so it is worth following a single event all the
way from the domain method that raises it to the consumer that handles it:

![Data-flow diagram of the domain-event path: an entity method queues an event and a [RaisesEvent] declaration is matched before the save, SaveChangesAsync commits, the after-save hook drains the queue and maps declared rules through IMapper, then every IEventPublisher receives the list and the bus publisher forwards each event to its consumer.](./diagrams/dknet-domain-event-path.svg)

The parts worth knowing before you rely on it:

- **Two ways in.** `AddEvent(instance)` queues a pre-built event object. `AddEvent<TEvent>()` and the
  `[RaisesEvent]` attribute instead map the entity onto the event type, so both require an `IMapper` registration —
  without one, the save throws `EventException` rather than silently dropping the event.
- **`[RaisesEvent]` is evaluated before the save, published after it.** Property narrowing
  (`[RaisesEvent(EventOperations.Updated, Properties = [...])]`) reads `EntityEntry.Property(...).IsModified`,
  which is only meaningful before `SaveChanges` completes. Two rules naming the same payload for the same
  operation raise it once.
- **Nothing is published until the write succeeds.** Collection happens in the before-save hook, publishing in the
  after-save hook, so a failed save publishes nothing.
- **Publisher failures are logged, not rethrown.** A publisher that throws does not fail the save — the row is
  committed and that event is lost. Treat in-process publishing as best-effort, and use a durable transport if a
  consumer must not miss an event.
- **In-process by default, on the bus by opt-in.** `AddEventPublisher<TDbContext, TImpl>()` registers your own
  publisher; `AddSlimBusEventPublisher<TDbContext>()` adds one that forwards each event onto SlimMessageBus, copying
  an `IEventItem`'s `AdditionalData` onto the message as case-insensitive headers.

---

## Specification Pattern

[`DKNet.EfCore.Specifications`](./EfCore/DKNet.EfCore.Specifications.md) — not a hand-rolled `IRepository<T>` — is
the current, supported way to query and persist through a `DbContext`. `IRepositorySpec` is not generic over the
entity: one injected instance serves every entity type in the `DbContext`, with the entity type inferred from the
`Specification<TEntity>` passed to each call.

```csharp
using DKNet.EfCore.Specifications;

services.AddSpecRepo<AppDbContext>();
```

Its signature feature is the **Dynamic Predicate Builder**, for building type-safe EF Core predicates from
`(propertyName, operation, value)` triples at runtime — the shape search/filter APIs need when criteria aren't known
at compile time:

```csharp
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Dynamics;
using DKNet.EfCore.Specifications.Extensions;
using LinqKit;

public sealed class ProductSearchSpec : Specification<Product>
{
    public ProductSearchSpec(decimal? minPrice)
    {
        var predicate = CreatePredicate(p => p.IsActive);   // ExpressionStarter<Product>

        if (minPrice is not null)
            predicate = predicate.DynamicAnd(nameof(Product.Price), Ops.GreaterThanOrEqual, minPrice);

        WithFilter(predicate);
    }
}

// One injected IRepositorySpec, any entity type:
var results = await repo.ToListAsync(new ProductSearchSpec(100m), cancellationToken);
```

The operations enum is `Ops` (in `DKNet.EfCore.Specifications.Dynamics`), and `DynamicAnd`/`DynamicOr` take the
property name, the operation, and the value directly — there is no fluent sub-builder. `.AsExpandable()` is **not**
something you add when querying through `IRepositorySpec`: `RepositorySpec<TDbContext>.Query<TEntity>` already calls
it before applying the specification. You only need it by hand when you build such a predicate and run it against a
raw `DbSet`/`IQueryable` yourself, bypassing the repository — LinqKit cannot expand the predicate into SQL without
it.

`DKNet.EfCore.Repos` / `DKNet.EfCore.Repos.Abstractions` (the older generic-repository packages) were removed; see
[`Migrating-Repos-To-Specifications.md`](./EfCore/Migrating-Repos-To-Specifications.md) for the call-site mapping
onto Specifications.

---

## Cross-Cutting Concerns

Audit, encryption, and authorization attach as opt-in interceptors on the same `DbContext`, rather than leaking
into domain or application code:

- [`DKNet.EfCore.AuditLogs`](./EfCore/DKNet.EfCore.AuditLogs.md) — captures an audit trail of entity changes, with
  `[SensitiveData]`-aware redaction.
- [`DKNet.EfCore.DataAuthorization`](./EfCore/DKNet.EfCore.DataAuthorization.md) — row-level, ownership-based
  filtering via EF Core global query filters.
- [`DKNet.EfCore.Encryption`](./EfCore/DKNet.EfCore.Encryption.md) — transparent column-level encryption via an EF
  Core value converter (independent of the hook pipeline).

The first two register against the same [`DKNet.EfCore.Hooks`](./EfCore/DKNet.EfCore.Hooks.md) pipeline
(`IBeforeSaveHookAsync`/`IAfterSaveHookAsync`) that `DKNet.EfCore.Events` uses, and each is wired in independently
via its own DI extension — there is no single "cross-cutting concerns" package to opt into.

---

## Related Documentation

- **[Getting Started](Getting-Started.md)** — prerequisites and a first working setup
- **[Configuration](Configuration.md)** — the registration conventions these packages share
- **[Examples](Examples/README.md)** — runnable implementations
- **[API Reference](API-Reference.md)** — index into the per-package API documentation
- **[Testing Strategy](Testing-Strategy.md)** — test stack and coverage targets
