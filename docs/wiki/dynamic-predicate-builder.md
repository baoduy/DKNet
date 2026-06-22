---
title: Dynamic Predicate Builder
category: EF Core Persistence
tags: [efcore, specifications, linqkit, predicate, dynamic-query]
---

## Summary

The Dynamic Predicate Builder is the signature feature of
`DKNet.EfCore.Specifications`. It builds runtime EF Core predicates from
`(propertyName, FilterOperation, value)` triples with type-, enum-, and array-safe
conversion, letting applications construct filters whose shape is unknown at compile
time. It composes directly with the [[specifications]] it lives beside.

## Required call shape

```csharp
var predicate = PredicateBuilder.New<Product>()
    .And(p => p.IsActive)
    .DynamicAnd(b => b.With("Price", FilterOperations.GreaterThan, 100m));

var results = await _db.Products.AsExpandable().Where(predicate).ToListAsync();
```

`.AsExpandable()` is **mandatory** — LinqKit cannot translate the dynamic predicate
without it, and expression expansion fails otherwise. `DynamicAnd` and `DynamicOr`
already null-handle internally, so manual null checks should not be reintroduced.

## How it works under the hood

`DynamicPredicateBuilderExtensions` validates property names and expressions, resolves
each property's type, adjusts the requested operation for the value type, and assembles
clause strings with enum and array validation. The supported operations are defined by
the `FilterOperations` enumeration (equality, comparison, contains, `in`, and so on).
Its behavior under nulls, empty inputs, and unusual operation combinations is locked
down by dedicated edge-case tests.

## Pitfalls to avoid

- Forgetting `.AsExpandable()` — expansion fails at query time.
- Materializing early (calling `ToList()` before `.Where(...)`) — wrong results and
  poor performance.
- Re-adding manual null checks that `DynamicAnd`/`DynamicOr` already handle.

This builder is applied through [[repositories]] and targets entities defined in
[[efcore-abstractions]].
