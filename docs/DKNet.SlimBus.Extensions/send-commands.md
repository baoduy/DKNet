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
`Result.Fail` so an API layer (see `DKNet.AspCore.Extensions`' response mapping) can translate it to HTTP 404.

`Fluents.Requests.IWithKey<TKey>` is a small companion interface (`TKey Id { get; set; }`) for requests addressed by
an identifier. [DKNet.SlimBus.Generators](../DKNet.SlimBus.Generators/index.md) puts it on the update and action
requests it emits, so shared code can read the key generically.

## 🧱 Where it fits

A write request is the trigger [auto-save](auto-save-dbcontext.md) keys off: only types implementing
`Fluents.Requests.INoResponse` or `IWitResponse<T>` are ever saved for you. `DKNet.AspCore.Extensions` maps these
same contracts to minimal-API endpoints (`MapPost`, `MapPut`, `MapDelete`, and the `*ById` forms for `IWithKey<TKey>`
requests) and turns a failed `IResultBase`/`IResult<T>` into a `ProblemDetails` response.

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
