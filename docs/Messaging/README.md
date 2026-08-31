# Messaging

CQRS and messaging glue built on [SlimMessageBus](https://github.com/zarusz/SlimMessageBus), wired for EF Core.

## Packages

| Package | Description |
|---|---|
| [`DKNet.SlimBus.Extensions`](./DKNet.SlimBus.Extensions.md) | Fluent command/query/event interfaces, an EF Core auto-save interceptor, and a domain-event-to-bus publisher — a lightweight, MediatR-free CQRS pipeline in front of `DKNet.EfCore` aggregates. |
| [`DKNet.SlimBus.Generators`](./DKNet.SlimBus.Generators.md) | Roslyn incremental source generator that emits a CRUD vertical slice (request records, handlers, and endpoint registration) from `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`-attributed entity members. |

## Install

```bash
dotnet add package DKNet.SlimBus.Extensions
dotnet add package DKNet.SlimBus.Generators
```

See the package page for wiring (`AddSlimBusEfCoreInterceptor`, `AddSlimBusEventPublisher`), the full interface
surface, and how it composes with `DKNet.EfCore.Events`/`DKNet.EfCore.Specifications`.
