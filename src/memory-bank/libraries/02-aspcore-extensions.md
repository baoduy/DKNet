# DKNet.AspCore.Extensions — AI Skill File

> **Package**: `DKNet.AspCore.Extensions`  
> **Minimum Version**: `10.0.0`  
> **Minimum .NET**: `net10.0`  
> **Source**: `src/AspNet/DKNet.AspCore.Extensions/` (HTTP result helpers) + `src/SlimBus/DKNet.AspCore.Extensions/` (endpoint mappers — same package, two source folders)

---

## Purpose

Provides one-liner ASP.NET Core Minimal API extension methods that wire `DKNet.SlimBus.Extensions` handlers to HTTP verbs, translate `FluentResults` outcomes to correct HTTP status codes, and attach standard OpenAPI error metadata automatically.

---

## When To Use

- ✅ Mapping any SlimBus CQRS command or query to a Minimal API endpoint
- ✅ Applying standard `400 / 401 / 403 / 404 / 409 / 429 / 500` OpenAPI metadata to endpoints
- ✅ Returning a paginated JSON response from a paged query handler

## When NOT To Use

- ❌ MVC controller actions — this library is Minimal API only
- ❌ Endpoints that do not use SlimBus handlers — write raw `app.MapGet(...)` delegates instead
- ❌ Custom status code logic — the library maps `Result` failures to 400/500; if you need bespoke codes, handle `IResult` directly in a custom delegate

---

## Installation

```bash
dotnet add package DKNet.AspCore.Extensions
```

---

## Setup / DI Registration

No DI registration required. The library provides extension methods on `RouteGroupBuilder`.

```csharp
// Program.cs
var app = builder.Build();

var api = app.MapGroup("/api/v1")
             .RequireAuthorization();     // optional — apply policies as needed

// Map product endpoints (see Usage Pattern below)
api.MapProductEndpoints();
```

---

## Key API Surface

| Method | HTTP Verb | Handler Contract |
|---|---|---|
| `app.MapPost<TCmd, TResponse>(route)` | POST | `Fluents.Requests.IWitResponse<TResponse>` → 201 Created |
| `app.MapPost<TCmd>(route)` | POST | `Fluents.Requests.INoResponse` → 200 OK |
| `app.MapGet<TQuery, TResponse>(route)` | GET | `Fluents.Queries.IWitResponse<TResponse>` → 200 / 404 |
| `app.MapGetPage<TQuery, TResponse>(route)` | GET | `Fluents.Queries.IWitPageResponse<TResponse>` → 200 + `PagedResponse<T>` |
| `app.MapPut<TCmd, TResponse>(route)` | PUT | `Fluents.Requests.IWitResponse<TResponse>` → 200 |
| `app.MapPut<TCmd>(route)` | PUT | `Fluents.Requests.INoResponse` → 200 |
| `app.MapPatch<TCmd, TResponse>(route)` | PATCH | `Fluents.Requests.IWitResponse<TResponse>` → 200 |
| `app.MapPatch<TCmd>(route)` | PATCH | `Fluents.Requests.INoResponse` → 200 |
| `app.MapDelete<TCmd, TResponse>(route)` | DELETE | `Fluents.Requests.IWitResponse<TResponse>` → 200 |
| `app.MapDelete<TCmd>(route)` | DELETE | `Fluents.Requests.INoResponse` → 200 |
| `routeBuilder.ProducesCommons()` | — | Adds 400/401/403/404/409/429/500 OpenAPI metadata |

---

## Usage Pattern

```csharp
// ── Endpoint registration file ───────────────────────────────────────────
using DKNet.AspCore.Extensions;

public static class ProductEndpoints
{
    /// <summary>Maps all product resource endpoints onto the provided route group.</summary>
    public static RouteGroupBuilder MapProductEndpoints(this RouteGroupBuilder app)
    {
        // POST /api/v1/products  — create
        app.MapPost<CreateProductCommand, ProductDto>("/products")
           .WithTags("Products")
           .WithSummary("Create a new product");

        // GET  /api/v1/products/{id} — single item (null → 404 automatic)
        app.MapGet<GetProductQuery, ProductDto>("/products/{Id}")
           .WithTags("Products");

        // GET  /api/v1/products — paged list
        app.MapGetPage<ListProductsQuery, ProductDto>("/products")
           .WithTags("Products");

        // PUT  /api/v1/products/{id} — full update
        app.MapPut<UpdateProductCommand, ProductDto>("/products/{Id}")
           .WithTags("Products");

        // DELETE /api/v1/products/{id} — void delete
        app.MapDelete<DeleteProductCommand>("/products/{Id}")
           .WithTags("Products");

        return app;
    }
}
```

---

## Anti-Patterns

```csharp
// ❌ WRONG — writing manual result-to-HTTP translation
app.MapPost("/products", async (IMessageBus bus, CreateProductCommand cmd) =>
{
    var result = await bus.Send(cmd);
    if (result.IsFailed) return Results.BadRequest(result.Errors);   // ← manual, error-prone
    return Results.Created($"/products/{result.Value!.Id}", result.Value);
});

// ✅ CORRECT — use the one-liner; HTTP translation is handled automatically
app.MapPost<CreateProductCommand, ProductDto>("/products");

// ❌ WRONG — forgetting ProducesCommons causes missing OpenAPI error docs
app.MapGet<GetProductQuery, ProductDto>("/products/{Id}")
   .Produces<ProductDto>();   // ← only documents success; error codes missing

// ✅ CORRECT — ProducesCommons is called automatically inside MapGet; do not add manually

// ❌ WRONG — using MapGet for a mutation (creates hidden side-effects on GET)
app.MapGet<DeleteProductCommand>("/products/{Id}/delete");  // ← GET must be safe/idempotent

// ✅ CORRECT
app.MapDelete<DeleteProductCommand>("/products/{Id}");
```

---

## Composes With

| Library | How |
|---|---|
| `DKNet.SlimBus.Extensions` | The handlers being mapped — `TCommand` / `TQuery` must implement `Fluents.*` interfaces |
| `DKNet.AspCore.Idempotency` | Chain `.WithIdempotency()` after `MapPost<>` / `MapPut<>` |

---

## Test Example

```csharp
// Uses TestContainers.MsSql-backed integration test environment.
public class ProductEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetProduct_ExistingId_Returns200WithDto()
    {
        // Arrange
        var client = factory.CreateClient();
        var id = await SeedProductAsync(factory);

        // Act
        var response = await client.GetAsync($"/api/v1/products/{id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProductDto>();
        dto.ShouldNotBeNull();
        dto!.Id.ShouldBe(id);
    }

    [Fact]
    public async Task GetProduct_UnknownId_Returns404()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/products/{Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
```

---

## Quick Decision Guide

- Use `MapPost/MapPut/MapPatch/MapDelete` for mutating commands.
- Use `MapGet` for single-item queries and `MapGetPage` for paged queries.
- Apply `.WithIdempotency()` only to state-mutating endpoints.

---

## Version

| Version | Notes |
|---|---|
| `10.0.0` | Initial documentation — C# 13 extension blocks for `RouteGroupBuilder` |
