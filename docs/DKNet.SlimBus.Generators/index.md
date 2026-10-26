# DKNet.SlimBus.Generators

Roslyn incremental source generator that turns `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`-attributed entity members into a full CRUD vertical slice — request records, SlimBus handlers, and minimal-API endpoint registration — for .NET developers building SlimBus-based services.

## ✨ Why use it?

- **The entity method is the only feature code you write.** Mark a constructor or method, and the request record, the handler, and the route appear at compile time.
- **Generated slices are the same shape as hand-written ones.** They implement the same `Fluents` contracts from [DKNet.SlimBus.Extensions](../DKNet.SlimBus.Extensions/index.md), so a generated and a hand-written feature sit in one pipeline with one set of interceptors.
- **Nothing to keep in sync.** No scaffolding is written to disk, so a renamed parameter or a new `[Range]` shows up in the request record on the next build instead of drifting.
- **Take over any operation at any time.** Declare your own handler for a generated request and the generator steps aside, reporting an informational diagnostic rather than a conflict.
- **Errors are compile errors.** A missing DTO, a non-public member, two `[CrudCreate]`s, or two members colliding on one route fail the build with a specific diagnostic id.

## 🚀 Quick Start

Use a .NET 10 web project.

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

Add `Product.cs`, `ProductDto.cs`, and then replace `Program.cs`. Mark the entity members — this is the only hand-written feature code:

```csharp
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
using Catalog;
using DKNet.EfCore.DtoGenerator;

namespace Api;

[GenerateDto(typeof(Product))]
public partial record ProductDto;
```

Then `Program.cs` — the full host wiring and a self-contained request — and run with `dotnet run`:

```csharp
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

Building with `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` set (and a `<CompilerGeneratedFilesOutputPath>`) writes the generated files above to that path for inspection — see [Generate CRUD requests](generate-crud-requests.md) for the exact output.

## 🏗️ Architecture

![The Domain and API projects feed CrudGenerator at compile time; the generated requests, handlers and endpoints it emits call DKNet.SlimBus.Extensions, DKNet.EfCore.Specifications, Mapster and, when referenced, DKNet.AspCore.Extensions, inside the host process.](diagrams/components.svg)

`CrudGenerator` runs once per compilation, inside the C# compiler — it never executes in the host process and ships no runtime assembly. Everything to the right of it in the diagram is ordinary generated C# that runs like any hand-written code, calling whatever `IRepositorySpec`, `IMapper` and bus registrations the host wires up. The diagram cannot show lifetimes or registration order — those come from the host's own `Program.cs`.

## 🔄 How it works

![CrudGenerator scans the compiling assembly and its non-framework references for CRUD-attributed members, resolves each entity's GenerateDto DTO and IEntity key, reports DKCRUDGEN001/002/006 and skips the entity when that fails, then emits the requests file, the handlers file, and the endpoints file only when DKNet.AspCore.Extensions is referenced.](diagrams/slimbus-generators-compile-flow.svg)

The `CompilationProvider.Select` step re-runs `CrudModelBuilder.Build` on every compilation, because it reads the compilation as a whole rather than one file at a time. What it produces (`CrudGenerationResult`) is value-equatable, so the later `RegisterSourceOutput` step only re-emits source when that result actually changed — an edit that leaves every CRUD-relevant member and DTO untouched still costs a rescan, but not regenerated files. DTO resolution is scoped to the compiling project only — a referenced `Domain` assembly's entities are scanned, but its `[GenerateDto]` types are not. Diagnostics other than DKCRUDGEN001/002/006 (member-level or route-level) do not stop the entity from emitting; see [Read the generator's diagnostics](read-diagnostics.md).

## 🧩 Features

- [Generate CRUD requests from attributed members](generate-crud-requests.md) — Turn a `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` member and its resolved `[GenerateDto]` DTO into a request record.
- [Generate handlers, unless a hand-written one wins](generate-handlers.md) — Emit a dispatch handler per request, stepping aside for a hand-written `IHandler` with the same request name.
- [Map CRUD endpoints](map-crud-endpoints.md) — Compose `DKNet.AspCore.Extensions` mappers into one `Map{Entity}Crud` extension, opt-in per project.
- [Name requests and routes](name-requests-and-routes.md) — The fixed rules behind every generated type name, file name, route segment and route name.
- [Refuse a delete](refuse-a-delete.md) — Hang a validation rule on the DELETE route through the generated `Delete{Entity}Request`.
- [Expose domain actions](expose-domain-actions.md) — Publish a named operation at its own route with `[CrudAction]`, with or without a request body.
- [Read the generator's diagnostics](read-diagnostics.md) — What each `DKCRUDGEN0xx` id means and whether it fails the build.

## 🔗 Related packages

- [DKNet.SlimBus.Extensions](../DKNet.SlimBus.Extensions/index.md) — reach for it to write the slices this generator does not cover, by hand, in the same shape; it also supplies the request/handler contracts, auto-save and event publishing the generated code calls.
- [DKNet.EfCore.Abstractions](../DKNet.EfCore.Abstractions/index.md) — reach for it for the `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` attributes (in `DKNet.EfCore.Abstractions.Attributes`) and the `IEntity<TKey>` every attributed entity must implement.
- [DKNet.EfCore.DtoGenerator](../DKNet.EfCore.DtoGenerator/index.md) — reach for it to declare the `[GenerateDto]` DTOs this generator resolves against.
- [DKNet.EfCore.Specifications](../DKNet.EfCore.Specifications/index.md) — reach for it for the `IRepositorySpec` and by-id specification surface every generated handler uses.
- [DKNet.AspCore.Extensions](../DKNet.AspCore.Extensions/index.md) — reach for it for the endpoint mappers and `CrudMapOptions` the generated endpoint file composes.
