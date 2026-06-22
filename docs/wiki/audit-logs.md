---
title: Audit Logs
category: EF Core Persistence
tags: [efcore, audit, hooks, tracking]
---

## Summary

DKNet's audit-log feature provides automatic tracking of entity changes. Entities opt
in with the sealed `[AuditLog]` marker attribute, and a SaveChanges interceptor records
audit information when those entities are created or updated. It is one of the
cross-cutting concerns built on the [[savechanges-hooks]] pipeline.

## Opting in

Applying `[AuditLog]` to an entity type flags it for inclusion in audit logging. The
attribute is a sealed marker carrying no configuration — its presence alone enables
tracking, keeping the domain model declarative and uncluttered.

## How tracking works

Because `AggregateRoot` extends `DomainEntity` with audit and concurrency support (see
[[aggregate-root]]), entities already carry created/updated metadata. The audit hook
runs in the before-SaveChanges phase to populate or capture that information for
tracked, flagged entities, so audit data is written consistently in the same
transaction as the change.

## Where it fits

Audit logging is independent of the other interceptor-based concerns —
[[column-encryption]] and [[data-authorization]] — and is enabled via its own DI
extension. The marker attribute is defined among the contracts in
[[efcore-abstractions]], reinforcing that auditing is a domain-declared,
infrastructure-implemented concern in line with [[onion-architecture]].
