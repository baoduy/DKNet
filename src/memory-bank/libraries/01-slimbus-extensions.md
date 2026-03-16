# DKNet.SlimBus.Extensions — AI Skill File

> **Package**: `DKNet.SlimBus.Extensions`  
> **Minimum Version**: `10.0.0`  
> **Minimum .NET**: `net10.0`  
> **Source**: `src/SlimBus/DKNet.SlimBus.Extensions/`

---

## Purpose

Provides strongly-typed CQRS handler interfaces and automatic EF Core `SaveChanges` behavior built on top of SlimMessageBus, so commands and queries can be defined as plain C# records/classes without infrastructure boilerplate.

---

## When To Use

- ✅ Implementing a command handler that creates, updates, or deletes data
- ✅ Implementing a query handler that returns a single item or a paged list
- ✅ Consuming domain events published via `DKNet.EfCore.Events`
- ✅ Any SlimMessageBus handler that touches EF Core (auto-save is included)

## When NOT To Use

- ❌ Simple utility logic with no HTTP surface — use a plain service class instead
- ❌ Long-running background processing — use `DKNet.AspCore.Tasks` (`IBackgroundJob`)
- ❌ Fire-and-forget messaging to an external broker without a request/response contract — use raw SlimMessageBus producers directly

---

## Installation

```bash
dotnet add package DKNet.SlimBus.Extensions
```

---

## Setup / DI Registration

```csharp
// Program.cs
using DKNet.SlimBus.Extensions;

services.AddSlimBusForEfCore(builder => builder
    .WithProviderMemory()                                    // swap for Azure SB / RabbitMQ in prod
    .AutoDeclareFrom(typeof(CreateProductHandler).Assembly) // discovers all handlers
    .AddJsonSerializer());
```

---

## Key API Surface

| Type | Role |
|---|---|
| `Fluents.Requests.IWitResponse<TResponse>` | Marker interface — request record that returns `IResult<TResponse>` |
| `Fluents.Requests.INoResponse` | Marker interface — fire-and-forget command (returns `IResultBase`) |
| `Fluents.Requests.IHandler<TRequest, TResponse>` | Handler for a command returning `IResult<TResponse>` |
| `Fluents.Requests.IHandler<TRequest>` | Handler for a void command |
| `Fluents.Queries.IWitResponse<TResponse>` | Marker interface — query that returns a single nullable `TResponse` |
| `Fluents.Queries.IWitPageResponse<TResponse>` | Marker interface — query that returns `IPagedList<TResponse>` |
| `Fluents.Queries.IHandler<TQuery, TResponse>` | Handler for a single-result query |
| `Fluents.Queries.IPageHandler<TQuery, TResponse>` | Handler for a paged query |
| `Fluents.EventsConsumers.IHandler<TEvent>` | Consumer for a domain event |

---

## Usage Pattern

```csharp
// ── 1. Command (POST — creates a product) ────────────────────────────────
/// <summary>Request to create a new product.</summary>
public record CreateProductCommand(string Name, decimal Price)
    : Fluents.Requests.IWitResponse<ProductDto>;

/// <summary>Handler — EF Core SaveChanges is called automatically after this returns Ok.</summary>
public class CreateProductHandler(IWriteRepository<Product> repo)
    : Fluents.Requests.IHandler<CreateProductCommand, ProductDto>
{
    public async Task<IResult<ProductDto>> Handle(
        CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product { Name = request.Name, Price = request.Price };
        await repo.AddAsync(product, cancellationToken);
        return Results.Ok(ProductDto.From(product));
    }
}

// ── 2. Query (GET — single item) ─────────────────────────────────────────
/// <summary>Query to fetch one product by ID.</summary>
public record GetProductQuery(Guid Id)
    : Fluents.Queries.IWitResponse<ProductDto>;

public class GetProductHandler(IReadRepository<Product> repo)
    : Fluents.Queries.IHandler<GetProductQuery, ProductDto>
{
    public async Task<ProductDto?> Handle(
        GetProductQuery request, CancellationToken cancellationToken)
        => await repo.Query(p => p.Id == request.Id)
                     .Select(p => ProductDto.From(p))
                     .FirstOrDefaultAsync(cancellationToken);
}

// ── 3. Paged Query (GET list) ─────────────────────────────────────────────
/// <summary>Query to list products, paged.</summary>
public record ListProductsQuery(int Page = 1, int PageSize = 20)
    : Fluents.Queries.IWitPageResponse<ProductDto>;

public class ListProductsHandler(IReadRepository<Product> repo)
    : Fluents.Queries.IPageHandler<ListProductsQuery, ProductDto>
{
    public async Task<IPagedList<ProductDto>> Handle(
        ListProductsQuery request, CancellationToken cancellationToken)
        => await repo.Query()
                     .Select(p => ProductDto.From(p))
                     .ToPagedListAsync(request.Page, request.PageSize, cancellationToken);
}
```

---

## Anti-Patterns

```csharp
// ❌ WRONG — calling SaveChanges manually inside a handler (double-save risk)
public async Task<IResult<ProductDto>> Handle(CreateProductCommand cmd, CancellationToken ct)
{
    await _repo.AddAsync(new Product { Name = cmd.Name }, ct);
    await _repo.SaveChangesAsync(ct);   // ← the framework already calls this after Handle()
    return Results.Ok(...);
}

// ✅ CORRECT — let the EfAutoSave interceptor handle it
public async Task<IResult<ProductDto>> Handle(CreateProductCommand cmd, CancellationToken ct)
{
    await _repo.AddAsync(new Product { Name = cmd.Name }, ct);
    return Results.Ok(...);
}

// ❌ WRONG — returning the entity directly (leaks domain model)
return Results.Ok(product);

// ✅ CORRECT — map to DTO before returning
return Results.Ok(ProductDto.From(product));

// ❌ WRONG — using ICommandHandler / IQueryHandler from MediatR
public class Handler : IRequestHandler<Command, Result<Dto>>  // MediatR
// ✅ CORRECT — use DKNet Fluents interfaces
public class Handler : Fluents.Requests.IHandler<Command, Dto>
```

---

## Composes With

| Library | How |
|---|---|
| `DKNet.AspCore.Extensions` | Maps these handlers to HTTP endpoints via `MapPost`, `MapGet`, etc. |
| `DKNet.EfCore.Repos` / `DKNet.EfCore.Repos.Abstractions` | Inject `IReadRepository<T>` / `IWriteRepository<T>` into handlers |
| `DKNet.EfCore.Specifications` | Build specifications inside query handlers for complex filtering |
| `DKNet.EfCore.Events` | Handlers can raise domain events; consumed by `EventsConsumers.IHandler<T>` |

---

## Test Example

```csharp
// Integration test: command handler persists and returns DTO
public class CreateProductHandlerTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private AppDbContext _db = null!;
    private IWriteRepository<Product> _repo = null!;

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder().WithPassword("YourStrong@Passw0rd1").Build();
        await _container.StartAsync();
        _db = CreateDbContext(_container.GetConnectionString());
        await _db.Database.EnsureCreatedAsync();
        _repo = new WriteRepository<Product>(_db);
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsAndReturnsDto()
    {
        // Arrange
        var handler = new CreateProductHandler(_repo);
        var command = new CreateProductCommand("Widget", 9.99m);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        await _db.SaveChangesAsync(); // simulate auto-save in test

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Name.ShouldBe("Widget");
        _db.Products.Count().ShouldBe(1);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        if (_container is not null) await _container.DisposeAsync();
    }
}
```

---

## Quick Decision Guide

- Use `Fluents.Requests.*` when the operation mutates state.
- Use `Fluents.Queries.*` when the operation is read-only.
- Use `Fluents.Queries.IWitPageResponse<T>` + page handler for paginated GET endpoints.

---

## Version

| Version | Notes |
|---|---|
| `10.0.0` | Initial documentation — `Fluents` class with nested `Requests`, `Queries`, `EventsConsumers` |
