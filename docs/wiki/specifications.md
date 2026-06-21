---
title: Specifications
category: EF Core Persistence
tags: [efcore, specifications, linqkit, query]
---

## Summary

`DKNet.EfCore.Specifications` provides composable query objects that encapsulate query
intent — `Criteria`, `Includes`, and `OrderBy` — and combine using LinqKit operators
such as `.And()` and `.Or()`. Specifications let query logic be named, reused, and
unit tested instead of being scattered through repositories and handlers.

## Anatomy of a specification

A specification bundles the parts of a query:

- **Criteria** — the filtering predicate (an `Expression<Func<T, bool>>`).
- **Includes** — related navigations to eager-load.
- **OrderBy** — the sort order to apply.

Multiple specifications compose with LinqKit's predicate operators, so small
single-purpose specifications can be `.And()`/`.Or()`-ed into a larger query without
rewriting the underlying expression trees.

## Dynamic predicates

For runtime-driven filtering (for example search APIs where the criteria are not known
at compile time), the package's signature feature is the
[[dynamic-predicate-builder]], which builds EF Core predicates from
`(propertyName, FilterOperation, value)` triples with type- and enum-safe conversion.

## Where specifications are used

Specifications are consumed by [[repositories]], which apply them against the
`DbContext`. Because they produce translatable EF Core expressions, they push
filtering to the database rather than into memory. They target the entity types
declared in [[efcore-abstractions]], keeping query reuse aligned with the domain
model of [[domain-driven-design]].
