# DKNet.SlimBus.Generators

A Roslyn incremental source generator that turns `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`-attributed entity members into a full CRUD vertical slice — request records, SlimBus handlers, and (optionally) minimal-API endpoint registration.

## ✨ Why use it?

- **The entity method is the only feature code you write.** Mark a constructor or method, and the request record, the handler, and the route appear at compile time.
- **Generated slices are the same shape as hand-written ones.** They implement the same `Fluents` contracts from `DKNet.SlimBus.Extensions`, so a generated and a hand-written feature sit in one pipeline with one set of interceptors.
- **Nothing to keep in sync.** No scaffolding is written to disk, so a renamed parameter or a new `[Range]` shows up in the request record on the next build instead of drifting.
- **Take over any operation at any time.** Declare your own handler for a generated request and the generator steps aside, reporting an informational diagnostic rather than a conflict.
- **Errors are compile errors.** A missing DTO, a non-public member, two `[CrudCreate]`s, or two members colliding on one route fail the build with a specific diagnostic id.

## 🚀 Quick Start

Use a .NET 10 web project. Install the packages, then add `Product.cs`, `ProductDto.cs`, and replace `Program.cs` with the example.

```bash
dotnet new web -f net10.0 -n QsApi
cd QsApi
dotnet add package DKNet.SlimBus.Generators
dotnet add package DKNet.EfCore.DtoGenerator
dotnet add package DKNet.SlimBus.Extensions
dotnet add package DKNet.EfCore.Specifications
dotnet add package DKNet.AspCore.Extensions
dotnet add package Mapster
dotnet add package SlimMessageBus.Host.Memory
dotnet add package SlimMessageBus.Host.Serialization.SystemTextJson
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.12
```

```csharp
// Product.cs — the only hand-written feature code
using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;

namespace Catalog;

public class Product : Entity
{
    private Product() { } // EF

    [CrudCreate]
    public Product([Required, MaxLength(100)] string name, decimal price)
    {
        Name = name;
        Price = price;
    }

    public string Name { get; private set; } = null!;
    public decimal Price { get; private set; }

    [CrudUpdate]
    public void UpdatePrice([Range(0, 1_000_000)] decimal price) => Price = price;
}
```

```csharp
// ProductDto.cs
using Catalog;
using DKNet.EfCore.DtoGenerator;

namespace Api;

[GenerateDto(typeof(Product))]
public partial record ProductDto;
```

```csharp
// Program.cs
using System.Net.Http.Json;
using Api;
using Catalog;
using DKNet.EfCore.Specifications;
using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QsApi.Crud;                                        // {AssemblyName}.Crud
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");

builder.Services.AddSingleton<IMapper>(new Mapper(new TypeAdapterConfig()));
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
builder.Services.AddScoped<DbContext>(p => p.GetRequiredService<AppDbContext>());
builder.Services.AddSpecRepo<AppDbContext>();          // DKNet.EfCore.Specifications

builder.Services
    .AddSlimBusEfCoreInterceptor<AppDbContext>()        // auto-save after a successful write
    .AddSlimMessageBus(mbb => mbb
        .AddJsonSerializer()
        .AddServicesFromAssembly(typeof(Program).Assembly)   // discovers generated + hand-written handlers
        .AddChildBus("Memory", mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(Program).Assembly)));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapGroup("/products").MapProductCrud();             // generated extension method

await app.StartAsync();

using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
var response = await client.PostAsJsonAsync("/products", new { name = "Coffee", price = 9.5m });
var dto = await response.Content.ReadFromJsonAsync<ProductDto>();
Console.WriteLine($"{(int)response.StatusCode} {response.StatusCode}: {dto}"); // 201 Created: ProductDto { Name = Coffee, Price = 9.5, Id = ... }

await app.StopAsync();

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}
```

Run with `dotnet run`.

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.SlimBus.Generators/index.md) | Start with architecture and the end-to-end flow. |
| [Generate CRUD requests from attributed members](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.SlimBus.Generators/generate-crud-requests.md) | Turn a `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` member and its resolved `[GenerateDto]` DTO into a request record. |
| [Generate handlers, unless a hand-written one wins](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.SlimBus.Generators/generate-handlers.md) | Emit a dispatch handler per request, stepping aside for a hand-written `IHandler` with the same request name. |
| [Map CRUD endpoints](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.SlimBus.Generators/map-crud-endpoints.md) | Compose `DKNet.AspCore.Extensions` mappers into one `Map{Entity}Crud` extension, opt-in per project. |
| [Name requests and routes](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.SlimBus.Generators/name-requests-and-routes.md) | The fixed rules behind every generated type name, file name, route segment and route name. |
| [Refuse a delete](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.SlimBus.Generators/refuse-a-delete.md) | Hang a validation rule on the DELETE route through the generated `Delete{Entity}Request`. |
| [Expose domain actions](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.SlimBus.Generators/expose-domain-actions.md) | Publish a named operation at its own route with `[CrudAction]`, with or without a request body. |
| [Read the generator's diagnostics](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.SlimBus.Generators/read-diagnostics.md) | What each `DKCRUDGEN0xx` id means and whether it fails the build. |
