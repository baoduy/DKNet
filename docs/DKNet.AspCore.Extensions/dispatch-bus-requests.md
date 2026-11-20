# Dispatch HTTP requests through SlimMessageBus

Part of [DKNet.AspCore.Extensions](index.md). The fluent mappers on `RouteGroupBuilder` resolve `IMessageBus` and dispatch the bound request. Register a bus and matching handlers in the host before serving these endpoints. This complete mapping example declares a command; it prints the endpoint name without dispatching it:

```csharp
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.SlimBus.Extensions;
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var group = app.MapGroup("/orders");
group.MapPost<CreateOrder, OrderModel>("/").WithName("CreateOrder");
Console.WriteLine("CreateOrder mapped; register IMessageBus and its handler before serving.");
public sealed record CreateOrder : Fluents.Requests.IWitResponse<OrderModel>;
public sealed record OrderModel(Guid Id);
```

![The fluent mapper binds a command, assigns any route key, sends it through IMessageBus and converts the returned result.](diagrams/bus-dispatch.svg)

| Mapper | Contract and binding | Success |
|---|---|---|
| `MapGet<TCommand,TResponse>` | [`Fluents.Queries.IWitResponse<TResponse>`](../DKNet.SlimBus.Extensions/run-queries.md), `[AsParameters]` | 200 value, 404 for null. |
| `MapGetPage<TCommand,TResponse>` | [`Fluents.Queries.IWitPageResponse<TResponse>`](../DKNet.SlimBus.Extensions/run-queries.md), `[AsParameters]` | 200 `PagedResponse<TResponse>`. |
| `MapPost<TCommand,TResponse>` / `MapPost<TCommand>` | [`Fluents.Requests.IWitResponse<TResponse>`](../DKNet.SlimBus.Extensions/send-commands.md) / [`INoResponse`](../DKNet.SlimBus.Extensions/send-commands.md), inferred body | 201 if type name contains `Create` case-insensitively; otherwise 200. |
| `MapPut` / `MapPatch`, with or without response | Same request contracts, inferred body | 200. |
| `MapDelete<TCommand,TResponse>` | Response request, explicit `[FromBody]` | 200. |
| `MapDelete<TCommand>` | No-response request, `[AsParameters]` | 200. |
| `MapPutById<TCommand,TKey,TResponse>` | Response request plus [`IWithKey<TKey>`](../DKNet.SlimBus.Extensions/send-commands.md), body and route id | 200; default route `{id}`. |
| `MapActionById<TCommand,TKey,TResponse>` | Same key contract; supplied route and verb, required body | 200. |
| `MapParameterlessActionById<TCommand,TKey,TResponse>` | Same contracts plus `new()`; supplied route and verb, no body binding | 200. |

By-ID forms assign the route value to `request.Id` before sending. Parameterless actions construct the command and ignore any supplied body; other members keep their defaults. Every mapper adds `ProducesCommons()`: standard error response metadata and an endpoint exception filter. You can also apply it to a hand-written route.

## ⚙️ Configuration reference

None — these mappers and `ProducesCommons()` read no options and expose no override point. `ProducesCommons()` always adds the same response metadata and the same unhandled-exception filter; register [Apply one error-response setting](shape-error-responses.md)'s `ErrorResponseOptions` to change the body shape of what that filter and the dispatched handler's failures produce.

## 🧱 Where it fits

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| `Map{Entity}Crud` (generated) | [SlimBus Generators](../DKNet.SlimBus.Generators/map-crud-endpoints.md) | Calls `MapPost`, `MapPutById`, `MapActionById` and `MapParameterlessActionById` to register the create, update and action routes it generates for an entity. |

Every mapper itself calls [`Response()`](convert-results.md) (same package) on the dispatched result to build its HTTP response, and resolves `IMessageBus` plus the [`IWitResponse<T>`/`IWithKey<TKey>`](../DKNet.SlimBus.Extensions/send-commands.md) and [`IWitPageResponse<T>`](../DKNet.SlimBus.Extensions/run-queries.md) contracts from [SlimBus Extensions](../DKNet.SlimBus.Extensions/index.md) — register the bus and handler pipeline there. Entity-only reads and deletes can instead use [repository mappers](map-entity-endpoints.md); both appear in the package's component diagram on the [package overview](index.md).

**Entry point in your app:** calling `group.MapPost<...>(...)` (or another mapper in the table above) on a `RouteGroupBuilder` in `Program.cs` or inside an `IEndpointConfig.Map`.

## ⚠️ Gotchas & limits

- Mapping is not bus registration. Missing `IMessageBus` or handler configuration fails at runtime.
- A POST command’s type name controls 201. The created-response location is a placeholder `/` for valued results.
- No-response DELETE uses parameter binding; response-bearing DELETE uses a required body.
- A parameterless action cannot populate body-only members. Use it only when the route id is sufficient.
- The common exception filter does not catch cancellation, aborted requests, or errors after the response has started.
