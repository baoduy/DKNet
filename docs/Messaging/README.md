# Messaging

CQRS and messaging glue built on [SlimMessageBus](https://github.com/zarusz/SlimMessageBus) — usable as an EF-free
MediatR replacement, with an EF Core add-on for auto-save and domain events.

## Packages

| Package | Description |
|---|---|
| [`DKNet.SlimBus.Extensions`](../DKNet.SlimBus.Extensions/index.md) | EF-free core: fluent command/query/event interfaces, `FluentResults` handlers, lazy Mapster mapping, and `AddSlimBusForMediator` — a one-call in-memory bus that replaces MediatR. |
| [`DKNet.SlimBus.Extensions.EfCore`](../DKNet.SlimBus.Extensions/auto-save-dbcontext.md) | EF Core add-on: an auto-save interceptor and a domain-event-to-bus publisher in front of `DKNet.EfCore` aggregates; `AddSlimBusForEfCore<TDbContext>` wires both on top of the core. |
| [`DKNet.SlimBus.Generators`](../DKNet.SlimBus.Generators/index.md) | Roslyn incremental source generator that emits a CRUD vertical slice (request records, handlers, and endpoint registration) from `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`-attributed entity members. |

## Install

```bash
# Without EF Core — MediatR replacement
dotnet add package DKNet.SlimBus.Extensions

# With EF Core — auto-save + domain events (brings DKNet.SlimBus.Extensions with it)
dotnet add package DKNet.SlimBus.Extensions.EfCore

dotnet add package DKNet.SlimBus.Generators
```

```csharp
services.AddSlimBusForMediator(typeof(Program).Assembly);              // without EF Core
services.AddSlimBusForEfCore<AppDbContext>(typeof(Program).Assembly);  // with EF Core
```

See the package pages for the full interface surface, the auto-save rules, and how the add-on composes with
`DKNet.EfCore.Events`/`DKNet.EfCore.Specifications`.
