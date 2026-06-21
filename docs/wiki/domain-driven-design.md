---
title: Domain-Driven Design in DKNet
category: Architecture & Concepts
tags: [ddd, aggregates, value-objects, ubiquitous-language]
---

## Summary

DKNet is built around Domain-Driven Design (DDD): the framework provides the
primitives needed to model a rich domain — aggregate roots, entities, value objects,
domain services, and domain events — and keeps that model free of infrastructure
concerns. DDD pairs with [[onion-architecture]] so the domain stays at the center
while persistence and messaging wrap around it.

## Building blocks

- **Aggregate roots** define consistency boundaries and are the only entry point for
  changing the entities they contain. DKNet's base type is described in
  [[aggregate-root]].
- **Entities and value objects** model identity-bearing and immutable concepts
  respectively (for example a `Money` record validating amount and currency).
- **Domain events** record meaningful state changes; see [[domain-events]].
- **Domain services** hold logic that does not belong to a single entity.
- **Specifications** encapsulate reusable query intent; see [[specifications]].

## Ubiquitous language and rich behavior

DKNet favors rich entities over anemic data bags: state is mutated through
intention-revealing methods (such as `Order.ConfirmOrder` or `Product.UpdatePrice`)
that enforce invariants and raise events, rather than through public setters. Code
uses the same terminology as domain experts, keeping the model aligned with the
business.

## Bounded contexts and persistence

Within a solution, models are organized into bounded contexts, each with its own
`DbContext` and entity set. Persistence is reached only through abstractions —
[[repositories]] consuming [[specifications]] — so domain code depends on interfaces,
never on EF Core directly. Application orchestration runs through [[cqrs-slimbus]]
handlers that load aggregates, invoke their behavior, and persist the result.
