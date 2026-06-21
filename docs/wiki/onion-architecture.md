---
title: Onion Architecture in DKNet
category: Architecture & Concepts
tags: [architecture, onion, dependency-inversion, layering]
---

## Summary

DKNet implements the Onion Architecture so that dependencies always flow inward
toward the domain. The framework expresses this not through a single application
project but at its **package boundaries**: domain abstractions sit at the core,
persistence and infrastructure adapters wrap around them, and consuming applications
opt in via dependency injection. The result is business logic that is independent of
EF Core, message buses, and storage providers.

## Layers and dependency flow

Concentric layers, from the inside out:

1. **Domain layer (core)** — entities, value objects, and domain logic with no
   infrastructure dependencies. Anchored by [[efcore-abstractions]] and the
   [[aggregate-root]] base type.
2. **Application layer** — orchestrates the domain through command/query handlers,
   validation, and DTOs. In DKNet this is realized with [[cqrs-slimbus]].
3. **Infrastructure layer** — implements interfaces declared by inner layers:
   [[repositories]], [[savechanges-hooks]], and the service adapters.
4. **Presentation layer** — API endpoints and controllers that depend only on the
   application layer.

Dependencies point inward only: the domain never references EF Core, and the
presentation layer never reaches past the application layer.

## How packages encode the layers

Because DKNet is a library suite, each ring is a separate NuGet package. The domain
contracts live in `DKNet.EfCore.Abstractions`; persistence concerns are layered as
opt-in EF Core interceptors ([[savechanges-hooks]]); and query logic is kept reusable
through [[specifications]]. This package-level separation is what lets a consuming
application swap an infrastructure adapter (for example a blob provider) without
touching domain or application code.

## Why it matters

Onion layering gives DKNet its testability and flexibility: domain rules are unit
testable in isolation, infrastructure is replaceable, and cross-cutting concerns
(audit, encryption, authorization) attach as interceptors rather than leaking into
business code. See [[domain-driven-design]] for the modeling concepts that fill these
layers and [[domain-events]] for how the rings communicate without tight coupling.
