---
title: Domain Events
category: Architecture & Concepts
tags: [domain-events, event-driven, efcore-events]
---

## Summary

Domain events represent facts that have happened in the domain. In DKNet they are
modeled as `DomainEvent` records (implementing `IDomainEvent`) raised by aggregates,
then dispatched by `DKNet.EfCore.Events` during `SaveChanges`. This gives DKNet an
event-driven core where bounded contexts and side effects stay loosely coupled from
the code that triggers them.

## The event type

`DomainEvent` is an abstract record implementing `IDomainEvent`. It computes a lazy
`HashId` via an abstract `GenerateHashId` hook, which supports deduplicating events.
Aggregates derive concrete events (for example `ProductPriceChangedEvent`) and attach
them by calling `AddEvent(...)` from inside their business methods — see
[[aggregate-root]].

## Dispatch during SaveChanges

`DKNet.EfCore.Events` collects the uncommitted events from tracked aggregates,
persists the database changes, and then publishes the events through an
`IEventPublisher`. The `DefaultEventPublisher` base supplies the dispatch plumbing.
Because dispatch is wired into the SaveChanges pipeline alongside the other
interceptors, it composes with [[savechanges-hooks]] rather than requiring manual
calls in handlers.

## Publishing to a message bus

When `DKNet.SlimBus.Extensions` is in use, the `SlimBusEventPublisher` implements
`IEventPublisher` by forwarding domain events to SlimMessageBus, mapping each event's
additional data onto message headers. This connects the in-process domain events to
the broader messaging infrastructure described in [[cqrs-slimbus]]. The events
originate from rich behavior modeled per [[domain-driven-design]].
