---
title: AggregateRoot
category: Architecture & Concepts
tags: [ddd, aggregate-root, domain, abstractions]
---

## Summary

`AggregateRoot` is the abstract base class in `DKNet.EfCore.Abstractions` that marks
the consistency boundary of an aggregate. It extends `DomainEntity` to add audit and
concurrency support, and it carries the domain events raised while business methods
execute. Rich domain entities derive from it and mutate state through methods that
call `AddEvent(...)`.

## What it provides

- **Consistency boundary** — an aggregate root is the single entry point for changing
  the entities inside its aggregate, ensuring invariants hold on every change.
- **Audit and concurrency** — by extending `DomainEntity`, it inherits created/updated
  tracking (`SetUpdatedBy`) and concurrency support, which integrates with the
  [[audit-logs]] feature.
- **Event collection** — it accumulates uncommitted [[domain-events]] added during
  business operations and exposes them so the persistence layer can dispatch them.

## Raising events from behavior

Entities call `AddEvent(...)` inside their domain methods rather than exposing
setters. For example, a `Product.UpdatePrice(...)` method validates the change,
updates the price, records the editor via `SetUpdatedBy`, and calls
`AddEvent(new ProductPriceChangedEvent(...))`. The events stay attached to the
aggregate until SaveChanges runs.

## Where it fits

`AggregateRoot` is the heart of DKNet's domain layer and is defined alongside the
other DDD contracts in [[efcore-abstractions]]. The events it accumulates are
dispatched automatically by the persistence pipeline — see [[domain-events]] and
[[savechanges-hooks]] — and aggregates are loaded and saved through [[repositories]].
