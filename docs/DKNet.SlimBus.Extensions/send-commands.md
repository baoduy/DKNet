# Send write commands

Part of [DKNet.SlimBus.Extensions](index.md). Mark a write with `Fluents.Requests.INoResponse` (no return value) or
`IWitResponse<TResponse>` (returns a value), then implement its handler.

Use the console-project setup in the [Quick Start](index.md#-quick-start), then replace `Program.cs` with this
example:

```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;

var services = new ServiceCollection();
services.AddLogging();
services.AddSlimMessageBus(mbb => mbb
    .AddServicesFromAssembly(typeof(Program).Assembly)
    .AddChildBus("Memory", m => m.WithProviderMemory().AutoDeclareFrom(typeof(Program).Assembly)));

await using var provider = services.BuildServiceProvider();
var bus = provider.GetRequiredService<IMessageBus>();

var created = await bus.Send(new CreateProduct("Widget", 9.99m));
Console.WriteLine($"CreateProduct -> IsSuccess={created.IsSuccess}, Id={created.Value}");

var deactivated = await bus.Send(new DeactivateProduct(Guid.NewGuid()));
Console.WriteLine($"DeactivateProduct (unknown id) -> IsSuccess={deactivated.IsSuccess}");

public record CreateProduct(string Name, decimal Price) : Fluents.Requests.IWitResponse<Guid>;

internal sealed class CreateProductHandler : Fluents.Requests.IHandler<CreateProduct, Guid>
{
    public Task<IResult<Guid>> OnHandle(CreateProduct request, CancellationToken cancellationToken) =>
        Task.FromResult<IResult<Guid>>(Result.Ok(Guid.NewGuid())); // a real handler persists the new Product first
}

public record DeactivateProduct(Guid ProductId) : Fluents.Requests.INoResponse;

internal sealed class DeactivateProductHandler : Fluents.Requests.IHandler<DeactivateProduct>
{
    public Task<IResultBase> OnHandle(DeactivateProduct request, CancellationToken cancellationToken) =>
        Task.FromResult<IResultBase>(Result.Fail(new NotFoundError("Product not found")));
    // a real handler looks the product up first and only fails when it is missing
}
```

Run with `dotnet run`. Prints `CreateProduct -> IsSuccess=True, Id=<guid>` then
`DeactivateProduct (unknown id) -> IsSuccess=False`.

`Fluents.Requests.IHandler<TRequest>` is `IRequestHandler<TRequest, IResultBase>` constrained to
`TRequest : INoResponse`, so `OnHandle` returns `Task<IResultBase>`. `Fluents.Requests.IHandler<TRequest, TResponse>`
returns `Task<IResult<TResponse>>`. A caller gets the same shape back from `IMessageBus.Send(...)`. With
[auto-save](auto-save-dbcontext.md) registered, a successful write saves the `DbContext` for you — the handler never
calls `SaveChangesAsync`.

![A command implementing INoResponse or IWitResponse<T> is sent through IMessageBus, the matching IHandler runs and returns a FluentResults value, and the caller inspects IsSuccess; a lookup miss returns Result.Fail(new NotFoundError(...)).](diagrams/send-commands.svg)

`NotFoundError` is a `FluentResults.Error` subclass for "the thing you addressed doesn't exist" — return it from
`Result.Fail` so an API layer (see [DKNet.AspCore.Extensions' response mapping](../DKNet.AspCore.Extensions/convert-results.md)) can translate it to HTTP 404.

`Fluents.Requests.IWithKey<TKey>` is a small companion interface (`TKey Id { get; set; }`) for requests addressed by
an identifier. [DKNet.SlimBus.Generators](../DKNet.SlimBus.Generators/generate-crud-requests.md) puts it on the
update and action requests it emits, so shared code can read the key generically.

## ⚙️ Configuration reference

There is no options type and no `IConfiguration` section.

| Override point | How you customise it |
|---|---|
| `Fluents.Requests.IHandler<TRequest>` / `IHandler<TRequest, TResponse>` | Implement it for your command; `AddServicesFromAssembly(...)` (see the [Quick Start](index.md#-quick-start)) discovers and registers it. |
| `Fluents.Requests.INoResponse` / `IWitResponse<TResponse>` | Implement one on your request record to mark it a write; which one decides the handler's return shape. |
| `Fluents.Requests.IWithKey<TKey>` | Implement it (`TKey Id { get; set; }`) on a request addressed by an identifier, so `*ById` endpoint mappers can bind the route key onto it. |

## 🧱 Where it fits

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| `FluentsEndpointMapperExtensions` | [DKNet.AspCore.Extensions](../DKNet.AspCore.Extensions/dispatch-bus-requests.md) | Maps `INoResponse`/`IWitResponse<T>` to `MapPost`/`MapPut`/`MapPatch`/`MapDelete`, and the `*ById` forms (`MapPutById`, `MapActionById`, `MapParameterlessActionById`) to the same contracts plus `IWithKey<TKey>`. |
| `FluentsEntityEndpointMapperExtensions` | [DKNet.AspCore.Extensions](../DKNet.AspCore.Extensions/map-entity-endpoints.md) | Its three-type `MapDeleteById<TEntity,TKey,TRequest>` binds a request implementing `IWithKey<TKey>` so a host validation filter can refuse a delete before the shared save helper runs. |
| Generated requests (emitted by `CrudGenerator`) | [DKNet.SlimBus.Generators](../DKNet.SlimBus.Generators/generate-crud-requests.md) | Implements `IWitResponse<TDto>` for every create/update/action request, and `IWithKey<TKey>` for update/action/delete requests. |

A write request is the trigger [auto-save](auto-save-dbcontext.md) keys off: only types implementing
`Fluents.Requests.INoResponse` or `IWitResponse<T>` are ever saved for you.

**Entry point in your app:** implement `Fluents.Requests.IHandler<TRequest[, TResponse]>` and let `AddServicesFromAssembly(...)` discover it; map it to HTTP with `MapPost`/`MapPut`/`MapPatch`/`MapDelete` from `DKNet.AspCore.Extensions`, or call `IMessageBus.Send(...)` yourself.

## ⚠️ Gotchas & limits

- **`Fluents.Requests.IWitResponse<T>` and `Fluents.Queries.IWitResponse<T>` are different interfaces** with the same
  name in different nested classes — one wraps `IResult<T>`, the other returns `T?` directly. Import them explicitly
  enough to keep them apart.
- **The handler never calls `SaveChangesAsync`** when [auto-save](auto-save-dbcontext.md) is registered; calling it
  yourself as well does no harm but is redundant.
- **A raw SlimMessageBus `IRequestHandler<,>` implementation is not a write for auto-save purposes.** Only
  `Fluents.Requests.INoResponse` / `IWitResponse<T>` are recognized — see
  [Auto-save after a successful write](auto-save-dbcontext.md).
- **Keep handlers stateless.** Don't cache request-specific state on the instance.
