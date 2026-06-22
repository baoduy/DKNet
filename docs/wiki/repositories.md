---
title: Repositories
category: EF Core Persistence
tags: [efcore, repository, persistence, read-write]
---

## Summary

`DKNet.EfCore.Repos` provides concrete generic repository implementations that
abstract persistence behind interfaces declared in `DKNet.EfCore.Repos.Abstractions`.
Repositories decouple domain and application code from EF Core and consume
[[specifications]] to express queries, keeping data access in the infrastructure ring
of [[onion-architecture]].

## Read/write separation

The abstractions package defines read and write repository interfaces separately, in
keeping with the CQRS approach. Read repositories expose query entry points
(typically returning `IQueryable`/`AsNoTracking` sequences for projection), while write
repositories add/update/delete aggregates and persist changes. Consuming code depends
on the interface it needs rather than a single fat repository.

## Consuming specifications and predicates

Repositories accept [[specifications]] to apply `Criteria`, `Includes`, and `OrderBy`
in one reusable unit, and they support the [[dynamic-predicate-builder]] for runtime
filtering. Filtering is pushed to the database, and reads default to `AsNoTracking()`
per the framework's EF Core conventions.

## Working with aggregates and events

Repositories load and save `AggregateRoot` instances defined in
[[efcore-abstractions]]. When changes are persisted, the SaveChanges pipeline applies
cross-cutting interceptors ([[savechanges-hooks]]) and dispatches [[domain-events]]. In
a SlimBus application the auto-save interceptor described in [[cqrs-slimbus]] often
triggers the save, so handlers fetch through a repository, mutate the aggregate, and
let persistence happen automatically.
