# Architecture Guide

This guide explains the architectural principles behind the DKNet Framework, and how they map onto its actual
packages and types — every type and API name below is real and grep-verified against `src/`, not illustrative.

## 📋 Table of Contents

- [Architectural Overview](#architectural-overview)
- [Domain-Driven Design (DDD)](#domain-driven-design-ddd)
- [Onion Architecture](#onion-architecture)
- [CQRS via DKNet.SlimBus.Extensions](#cqrs-via-dknetslimbusextensions)
- [Event-Driven Architecture](#event-driven-architecture)
- [Specification Pattern](#specification-pattern)
- [Cross-Cutting Concerns](#cross-cutting-concerns)

---

## 🏗️ Architectural Overview

DKNet Framework is a suite of independent .NET NuGet packages built around **Domain-Driven Design (DDD)** and the
**Onion Architecture** pattern. Rather than a single application skeleton, DKNet expresses these patterns at
**package boundaries**: each ring of the onion is a separate, opt-in package, so a consuming application pulls in
only what it needs and can swap an implementation (a blob provider, an idempotency store) without touching domain
code.

![Package dependency map of the DKNet onion: presentation, application and infrastructure packages all depend inward toward DKNet.EfCore.Abstractions, with Events, AuditLogs and DataAuthorization attaching through DKNet.EfCore.Hooks.](./diagrams/dknet-onion-packages.svg)

The rings above are packages, and every arrow is a real project reference in `src/`: dependencies only ever point
inward, toward `DKNet.EfCore.Abstractions`. `DKNet.Svc.*` (blob storage, encryption, PDF, transformation) sits in the
application ring alongside `DKNet.SlimBus.Extensions` but has no dependency on the EF Core rings at all, which is why
it carries no arrow here. `DKNet.EfCore.Encryption` is likewise absent: it attaches to the `DbContext` as a value
converter rather than through the hook pipeline.

---

## 🎯 Domain-Driven Design (DDD)

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

## 🧅 Onion Architecture

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

## ⚡ CQRS via DKNet.SlimBus.Extensions

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

## 🔄 Event-Driven Architecture

Domain events (raised via `AddEvent(...)` on `Entity`/`AuditedEntity`) enable loose coupling between bounded
contexts. [`DKNet.EfCore.Events`](./EfCore/DKNet.EfCore.Events.md) dispatches them automatically as part of the
[`DKNet.EfCore.Hooks`](./EfCore/DKNet.EfCore.Hooks.md) before/after-SaveChanges pipeline — application code never
calls a dispatcher directly. `[RaisesEvent]` attributes on an entity can additionally declare events raised
automatically on Created/Updated/Deleted, with optional property narrowing (see
[`DKNet.EfCore.DtoGenerator`](./EfCore/DKNet.EfCore.DtoGenerator.md) for the source-generator side of that).

If you want domain events forwarded onto the message bus (rather than only handled in-process), add
`AddSlimBusEventPublisher<TDbContext>()` from `DKNet.SlimBus.Extensions`.

---

## 📊 Specification Pattern

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
var predicate = PredicateBuilder.New<Product>()
    .And(p => p.IsActive)
    .DynamicAnd(b => b.With("Price", FilterOperations.GreaterThan, 100m));

var results = await db.Products.AsExpandable().Where(predicate).ToListAsync();
```

`.AsExpandable()` is mandatory — LinqKit cannot translate the predicate without it.

`DKNet.EfCore.Repos` / `DKNet.EfCore.Repos.Abstractions` (the older generic-repository packages) are retired and
superseded by Specifications; see
[`Migrating-Repos-To-Specifications.md`](./EfCore/Migrating-Repos-To-Specifications.md) for the call-site mapping.

---

## 🔧 Cross-Cutting Concerns

Audit, encryption, and authorization attach as opt-in SaveChanges interceptors on the same `DbContext`, rather than
leaking into domain or application code:

- [`DKNet.EfCore.AuditLogs`](./EfCore/DKNet.EfCore.AuditLogs.md) — captures an audit trail of entity changes, with
  `[SensitiveData]`-aware redaction.
- [`DKNet.EfCore.DataAuthorization`](./EfCore/DKNet.EfCore.DataAuthorization.md) — row-level, ownership-based
  filtering via EF Core global query filters.
- [`DKNet.EfCore.Encryption`](./EfCore/DKNet.EfCore.Encryption.md) — transparent column-level encryption via an EF
  Core value converter (independent of the hook pipeline).

Each registers against the same [`DKNet.EfCore.Hooks`](./EfCore/DKNet.EfCore.Hooks.md) pipeline
(`IBeforeSaveHookAsync`/`IAfterSaveHookAsync`) that `DKNet.EfCore.Events` uses, and each is wired in independently via
its own DI extension — there is no single "cross-cutting concerns" package to opt into.

---

## 📖 Related Documentation

- **[Getting Started](Getting-Started.md)** - Quick start guide
- **[Configuration](Configuration.md)** - Setup and configuration
- **[Examples](Examples/README.md)** - Practical implementations
- **[API Reference](API-Reference.md)** - Detailed API documentation
- **[Testing Strategy](Testing-Strategy.md)** - Test stack and coverage targets
