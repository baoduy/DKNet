# Auto-save after a successful write

Part of [DKNet.SlimBus.Extensions](index.md). `AddSlimBusEfCoreInterceptor<TDbContext>()` registers an interceptor
that saves a registered `DbContext` for you after a successful write — no `SaveChangesAsync` call in the handler.
Use a .NET 10 console project, then replace `Program.cs` with this example:

```bash
dotnet new console -f net10.0 -n AutoSaveDemo
cd AutoSaveDemo
dotnet add package DKNet.SlimBus.Extensions
dotnet add package SlimMessageBus.Host.Memory --version 3.5.0
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.12
```

```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;

await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
services.AddSlimBusEfCoreInterceptor<AppDbContext>();
services.AddSlimMessageBus(mbb => mbb
    .AddServicesFromAssembly(typeof(Program).Assembly)
    .AddChildBus("Memory", m => m.WithProviderMemory().AutoDeclareFrom(typeof(Program).Assembly)));

await using var provider = services.BuildServiceProvider();
await using (var scope = provider.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
}

var bus = provider.GetRequiredService<IMessageBus>();
var result = await bus.Send(new CreateProduct("Saved automatically", 1m));

// Prove it: a fresh scope sees the row, even though the handler never called SaveChangesAsync.
await using var verifyScope = provider.CreateAsyncScope();
var saved = await verifyScope.ServiceProvider.GetRequiredService<AppDbContext>().Products.FindAsync(result.Value);
Console.WriteLine($"Product found in a fresh scope = {saved is not null}");

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}

public sealed class Product
{
    public Product(Guid id, string name, decimal price)
    {
        Id = id;
        Name = name;
        Price = price;
    }

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public record CreateProduct(string Name, decimal Price) : Fluents.Requests.IWitResponse<Guid>;

internal sealed class CreateProductHandler(AppDbContext db) : Fluents.Requests.IHandler<CreateProduct, Guid>
{
    public async Task<IResult<Guid>> OnHandle(CreateProduct request, CancellationToken cancellationToken)
    {
        var product = new Product(Guid.NewGuid(), request.Name, request.Price);
        await db.Products.AddAsync(product, cancellationToken);
        // No SaveChangesAsync call here — the auto-save interceptor does it after this returns Ok.
        return Result.Ok(product.Id);
    }
}
```

Run with `dotnet run`. Prints `Product found in a fresh scope = True`.

`AddSlimBusEfCoreInterceptor<TDbContext>()` registers `EfAutoSavePostInterceptor<,>` as a SlimMessageBus
`IRequestHandlerInterceptor<,>`, so it needs the host's `AddSlimMessageBus(...)` call to actually dispatch through
it.

![AddSlimBusEfCoreInterceptor<TDbContext>() always adds the DbContext to the save registry; it registers EfAutoSavePostInterceptor as IRequestHandlerInterceptor<,> only when no IRequestHandlerInterceptor<,> is already registered, otherwise the interceptor is silently skipped.](diagrams/auto-save-dbcontext.svg)

## ⚙️ Configuration reference

There is no options type and no `IConfiguration` section — what you vary is which `DbContext` you call it with, and
when.

| Concern | Value |
|---|---|
| Saved for | `Fluents.Requests.INoResponse` and `Fluents.Requests.IWitResponse<T>` only |
| Skipped when | The response is `null`, or is an `IResultBase` with `IsSuccess == false` |
| Saved contexts | Every `DbContext` type registered through `AddSlimBusEfCoreInterceptor<T>()`, across every call, whose `ChangeTracker.HasChanges()` |
| Save call | `AddNewEntitiesFromNavigations`, then `SaveChangesWithConcurrencyHandlingAsync` (both from `DKNet.EfCore.Extensions`) |
| Exception handler | `IEfCoreExceptionHandler` keyed by the `DbContext`'s `FullName` (`AddEfCoreExceptionHandler<TDbContext, THandler>()`), falling back to an unkeyed registration |
| Interceptor order | `int.MaxValue` (`IInterceptorWithOrder`) — runs last in the chain, wrapping the handler directly, inside any interceptor you register |

Calling `AddSlimBusEfCoreInterceptor<TDbContext>()` again for the same `TDbContext` adds no second save registration.
For a second `DbContext`, call it again with that type — each registered context with pending changes is saved in
the same pass, independently.

## 🧱 Where it fits

[Send write commands](send-commands.md) is what gets saved; [Run read queries](run-queries.md) is never auto-saved.
[Publish domain events onto the bus](publish-domain-events.md) runs as part of the same `SaveChangesAsync` call this
interceptor triggers, through `DKNet.EfCore.Events`' save hook.

## ⚠️ Gotchas & limits

- **Registering your own open-generic `IRequestHandlerInterceptor<,>` before this one silently skips the auto-save
  interceptor.** `AddSlimBusEfCoreInterceptor<TDbContext>()` checks whether an open-generic
  `IRequestHandlerInterceptor<,>` registration — the same shape as
  `services.AddScoped(typeof(IRequestHandlerInterceptor<,>), typeof(YourInterceptor<,>))` — already exists on the
  service collection, and if one does, it still adds the `DbContext` to the save registry but never registers
  `EfAutoSavePostInterceptor`. The result compiles and runs with no warning; writes simply stop being saved. Call
  `AddSlimBusEfCoreInterceptor<TDbContext>()` before registering your own open-generic interceptors.
- **Query handlers that mutate the `DbContext` are not saved.** Auto-save only fires for `INoResponse` /
  `IWitResponse<T>` requests — by design, but easy to trip over when a "read" handler writes.
- **Raw SlimMessageBus requests bypass auto-save entirely.** A handler implementing `IRequestHandler<,>` directly,
  not through `Fluents.Requests`, is never recognized as a write.
- **No cross-`DbContext` transaction.** Each registered context with pending changes is saved independently in a
  loop; if a later save throws, earlier ones have already committed.
- **The `DbContext` type registry is scoped to the `IServiceCollection`.** Each registration is resolved from the
  provider built out of that collection, so separately built providers (parallel test fixtures, for instance) never
  see each other's registered types.
- **A `null` or failed response silently skips the save**, including an exception the handler caught and turned
  into `Result.Fail(...)`. This package adds no exception handling of its own around the handler call.
- **Auto-save runs after the handler returns, inside the interceptor.** A `DbUpdateException` surfaces from the
  interceptor, not the handler, so handler-local try/catch never sees it.
