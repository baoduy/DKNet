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

## Declared events via `[RaisesEvent]`

Entities can also *declare* events instead of hand-raising them, as two separate
declarations: a `[GenerateDto]` payload record (from [[dto-generator]]) shapes the
event, and the repeatable `DKNet.EfCore.Abstractions.Events.RaisesEventAttribute`
on the entity names that payload, the persistence operation(s), and an optional
update-narrowing property list. `DKNet.EfCore.Events` reads `[RaisesEvent]` via
reflection, evaluates narrowing against `EntityEntry.Property(...).IsModified` before
`SaveChanges`, then maps the entity onto the payload type via the registered
`IMapper` and publishes it after a successful save — coexisting with any hand-raised
events on the same entity. No `IEventEntity` or `AggregateRoot` base class is required
to declare, and a domain project referencing only `DKNet.EfCore.Abstractions` and
`DKNet.EfCore.DtoGenerator` builds fine with rules declared before the application
ever registers the event runtime. One limitation: a change confined to a nested owned
value does not raise the owner's update event, since EF Core does not report the
owner itself as `Modified` in that case.
