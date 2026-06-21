---
title: Data Authorization
category: EF Core Persistence
tags: [efcore, authorization, query-filters, multi-tenant, security]
---

## Summary

`DKNet.EfCore.DataAuthorization` enforces ownership-based, row-level data filtering
through EF Core global query filters. It restricts which rows a caller can see based on
ownership keys, providing multi-tenant or per-owner isolation without each query having
to remember to filter. It references the EfCore Extensions and Hooks projects and is a
cross-cutting concern in the [[savechanges-hooks]] family.

## Ownership model

An `IDataOwnerProvider` supplies the current owner key and the set of keys the caller
may access. Global query filters use this provider to scope reads to authorized rows
automatically. The persistence pipeline also stamps ownership on entities so that
written data carries the correct owner.

## Query-filter translation

A documented fix in the package addresses an EF Core global query-filter translation
issue, ensuring the ownership predicate translates to SQL rather than evaluating in
memory. This keeps authorization both correct and efficient, consistent with DKNet's
rule of pushing filtering to the database — the same principle behind
[[specifications]] and the [[dynamic-predicate-builder]].

## Where it fits

Like [[audit-logs]] and [[column-encryption]], data authorization is opt-in via a DI
extension and layers onto the `DbContext` without changing domain code. It operates on
the entities defined in [[efcore-abstractions]], upholding the inward dependency rule
of [[onion-architecture]].
