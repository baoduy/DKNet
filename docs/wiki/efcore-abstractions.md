---
title: DKNet.EfCore.Abstractions
category: EF Core Persistence
tags: [efcore, abstractions, ddd, aggregate-root, domain-events]
---

## Summary

`DKNet.EfCore.Abstractions` holds the DDD contracts and primitives at the core of the
DKNet domain model: the `AggregateRoot`/`Entity` base types and the domain-event base
types. It is the package that anchors a domain in [[domain-driven-design]] terms while
remaining free of concrete persistence logic.

## Core contracts

- **`AggregateRoot`** — abstract base marking an aggregate's consistency boundary,
  extending `DomainEntity` with audit and concurrency support and carrying domain
  events. Detailed in [[aggregate-root]].
- **`DomainEntity`** — the entity base that `AggregateRoot` forwards construction to,
  supplying identity and audit fields.
- **`DomainEvent` / `IDomainEvent`** — the abstract event record (with a lazily
  computed `HashId`) and its interface, covered in [[domain-events]].
- **Marker attributes** such as `[AuditLog]` and `[Encrypted]` that opt entities and
  properties into cross-cutting behaviors.

## Why it is separate

Keeping these contracts in their own package lets the domain layer depend on
abstractions only. Persistence behavior — repositories, hooks, event dispatch — lives
in sibling packages that reference these abstractions, which is the inward dependency
rule of [[onion-architecture]].

## How it is consumed

[[repositories]] persist `AggregateRoot` instances; [[savechanges-hooks]] interceptors
read the marker attributes to apply audit, encryption, and authorization; and
[[specifications]] query objects target the entity types defined here. The DTO
tooling in [[dto-generator]] can project these entities into transfer objects.
