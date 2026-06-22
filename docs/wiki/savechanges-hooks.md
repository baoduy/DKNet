---
title: SaveChanges Hooks
category: EF Core Persistence
tags: [efcore, hooks, interceptors, savechanges, cross-cutting]
---

## Summary

`DKNet.EfCore.Hooks` provides a before/after `SaveChanges` interceptor pipeline layered
onto the `DbContext`. Hooks are the mechanism by which DKNet attaches cross-cutting
concerns — [[audit-logs]], [[column-encryption]], and [[data-authorization]] — to
persistence without leaking that logic into domain or application code.

## The interceptor pipeline

Hooks run around the EF Core `SaveChanges` call: a *before* phase can inspect or mutate
tracked entities prior to writing (for example stamping audit fields or encrypting
properties), and an *after* phase runs once the write succeeds. Each concern is an
independent, opt-in hook registered via a DI extension on the consuming application,
so an app enables only the behaviors it needs.

## Cross-cutting concerns built on hooks

- **[[audit-logs]]** — automatic created/updated tracking for entities marked with
  `[AuditLog]`.
- **[[column-encryption]]** — encrypts/decrypts string properties marked `[Encrypted]`
  as they cross the persistence boundary.
- **[[data-authorization]]** — ownership-based row-level filtering applied through EF
  Core global query filters.

## Relationship to domain events

Domain-event dispatch (`DKNet.EfCore.Events`) participates in the same SaveChanges
lifecycle, collecting events from aggregates and publishing them after a successful
save — see [[domain-events]]. Because hooks and event dispatch share the pipeline, all
of these behaviors compose cleanly on a single `DbContext` and operate on the entities
and attributes defined in [[efcore-abstractions]].
