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
