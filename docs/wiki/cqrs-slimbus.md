---
title: CQRS with SlimBus
category: Architecture & Concepts
tags: [cqrs, slimbus, slimmessagebus, messaging, result-pattern]
---

## Summary

`DKNet.SlimBus.Extensions` provides DKNet's CQRS and messaging glue on top of
SlimMessageBus. Commands and queries are dispatched to handlers implementing
`IRequestHandler<TCommand, TResult>`, results use the FluentResults pattern, and an
EF Core auto-save interceptor persists changes after a successful handler. It is the
application layer of [[onion-architecture]], sitting between presentation and the
domain.

## Handlers and the result pattern

Handlers implement `IRequestHandler<TCommand, TResult>` for writes and equivalent
query/paged-query handlers for reads (the package references `X.PagedList.EF` for
paging). Commands load an aggregate through [[repositories]], invoke its behavior, and
return a `FluentResults` result that signals success or failure without throwing for
expected business outcomes.

## Automatic EF Core persistence

`EfAutoSavePostInterceptor` is a SlimMessageBus post-processing interceptor that calls
`SaveChanges` on the EF Core `DbContext` after a handler completes — but only when the
result indicates success, skipping persistence on failure. A registration table
tracks which `DbContext` types have auto-save enabled, so handlers don't have to call
`SaveChangesAsync` themselves. `SlimBusEfCoreSetup` is the DI extension that wires this
up.

## Domain events over the bus

Because the package references `DKNet.EfCore.Events`, domain events raised by
aggregates flow out through the `SlimBusEventPublisher`, which forwards them to
SlimMessageBus with their additional data as headers. This ties command handling to
the event-driven design in [[domain-events]], and keeps aggregates — described in
[[aggregate-root]] — at the center of each operation.
