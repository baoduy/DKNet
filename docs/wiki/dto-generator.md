---
title: DTO Generator
category: EF Core Persistence
tags: [efcore, source-generator, roslyn, dto, codegen]
---

## Summary

DKNet's DtoGenerator is a Roslyn **incremental source generator** that produces data
transfer objects from entities at compile time. A partial record or class is marked
with the `[GenerateDto]` attribute, and the generator emits the corresponding DTO,
removing hand-written, error-prone mapping boilerplate.

## The `[GenerateDto]` attribute

`[GenerateDto]` is an auto-included attribute carrying the target entity type plus
configuration options:

- **Exclude** — properties to omit from the generated DTO.
- **Include** — properties to explicitly include.
- **IgnoreComplexType** — control over how complex/navigation types are handled.

The README also documents global exclusions for properties that should never be
projected.

## Why a source generator

As an incremental generator, it runs as part of compilation and regenerates only when
relevant inputs change, so DTOs stay in sync with their source entities without a
runtime mapping library. The generated DTOs are produced from the domain entities
declared in [[efcore-abstractions]].

## How it is used in practice

DTOs generated here are typically returned by query handlers in [[cqrs-slimbus]] and
projected from queries built with [[specifications]], so read paths return shaped
transfer objects rather than exposing aggregates directly. This keeps the presentation
contract decoupled from the domain model, consistent with [[onion-architecture]].

## Validating raise rules with `[RaisesEvent]`

Declaring a domain event is two separate steps: a `[GenerateDto]` payload record shapes
the event (no different from any other generated DTO), and the repeatable
`DKNet.EfCore.Abstractions.Events.RaisesEventAttribute` on the entity names that payload,
the persistence operation(s) (`Created` / `Updated` / `Deleted`, combinable), and an
optional update-narrowing property list. This generator emits **no code** for
`[RaisesEvent]` — only build-time diagnostics: the named payload must be generated from
the same entity carrying the rule, and narrowing entries must be direct properties of
the entity. `DKNet.EfCore.Events` reads `[RaisesEvent]` via reflection at runtime and
raises the payload automatically after a successful save — see [[domain-events]] for the
raising side and the nested-owned-value limitation.

`[RaisesEvent]` also has a string form — naming an event that doesn't exist yet, e.g.
`[RaisesEvent("CustomerTouched", EventOperations.Created)]` — for which this generator
itself emits the payload: a public, partial, default-shape record in the carrying
entity's namespace (same shape as `[GenerateDto(typeof(Entity))]` with no
`Include`/`Exclude`/`IgnoreComplexType` knobs). New diagnostics guard it: `DKRAISEVT004`
(name already resolves to an existing, incompatible type), `DKRAISEVT005` (name isn't a
compile-time constant or a single valid identifier), and `DKRAISEVT006` (two entities in
the same namespace name the same event). A hand-authored `partial record` with the same
name merges with the generated one rather than colliding — the same pattern used to
extend any other generated DTO.
