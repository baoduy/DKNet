using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using AspCore.Idempotency.ApiTests;
using DKNet.AspCore.Idempotency;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON serialization options
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = false;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

builder.Services.AddDistributedMemoryCache();
builder.Services
    .AddAuthentication(TestAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);

var app = builder.Build();

app.UseAuthentication();

// Sample POST endpoint that requires idempotency
app.MapPost("/api/items", async (CreateItemRequest request) =>
    {
        // Simulate processing
        await Task.Delay(100);
        return TypedResults.Created(
            $"/api/items/{Guid.NewGuid()}",
            new CreateItemResponse
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                CreatedAt = DateTimeOffset.UtcNow
            });
    })
    .WithName("CreateItem")
    .WithDescription("Creates a new item. Requires idempotency key.")
    .RequiredIdempotentKey();

// Sample GET endpoint
app.MapGet("/api/health", () => TypedResults.Ok(new { status = "healthy" }))
    .WithName("Health");

// Endpoint whose result status code falls outside the configured caching range - exercises the
// "not configured for caching" path in the idempotency filter.
app.MapPost("/api/rejects", () => TypedResults.NotFound())
    .WithName("Reject")
    .WithDescription("Always returns 404. Requires idempotency key but the response is never cached.")
    .RequiredIdempotentKey();

// Tracks how many times DELETE /api/orders/{id} actually ran its side effect, so group-coverage
// tests can prove an uncovered endpoint re-processes a repeated key instead of just checking status codes.
var cancelCounts = new ConcurrentDictionary<string, int>();

// Orders-shaped group: default declaration covers POST only.
var orders = app.MapGroup("/api/orders").RequiredIdempotentKey();

orders.MapPost("/", async (CreateItemRequest request) =>
    {
        await Task.Delay(10);
        return TypedResults.Created(
            $"/api/orders/{Guid.NewGuid()}",
            new CreateItemResponse { Id = Guid.NewGuid(), Name = request.Name, CreatedAt = DateTimeOffset.UtcNow });
    })
    .WithName("CreateOrder"); // POST -> covered by the group declaration only

orders.MapGet("/", () => TypedResults.Ok(Array.Empty<CreateItemResponse>()))
    .WithName("ListOrders"); // GET -> outside the POST-only set, left untouched

orders.MapGet("/{id}", (string id) => TypedResults.Ok(new { id }))
    .WithName("GetOrder"); // GET -> left untouched

orders.MapPut("/{id}", async (string id, CreateItemRequest request) =>
    {
        await Task.Delay(10);
        return TypedResults.Ok(new CreateItemResponse
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            CreatedAt = DateTimeOffset.UtcNow
        });
    })
    .WithName("UpdateOrder"); // PUT -> not named in the group's verb set, left untouched

orders.MapDelete("/{id}", (string id) =>
    {
        var count = cancelCounts.AddOrUpdate(id, 1, (_, existing) => existing + 1);
        return TypedResults.Ok(new { id, cancelCount = count });
    })
    .WithName("CancelOrder"); // DELETE -> not named in the group's verb set: a repeated key must re-run this

orders.MapPost("/checkout", async (CreateItemRequest request) =>
    {
        await Task.Delay(10);
        return TypedResults.Created(
            $"/api/orders/checkout/{Guid.NewGuid()}",
            new CreateItemResponse { Id = Guid.NewGuid(), Name = request.Name, CreatedAt = DateTimeOffset.UtcNow });
    })
    .WithName("CheckoutOrder")
    .RequiredIdempotentKey(); // POST -> covered by both the group AND this per-endpoint declaration

orders.MapGroup("/{id}/refunds")
    .MapPost("/", async (string id, CreateItemRequest request) =>
    {
        await Task.Delay(10);
        return TypedResults.Created(
            $"/api/orders/{id}/refunds/{Guid.NewGuid()}",
            new CreateItemResponse { Id = Guid.NewGuid(), Name = request.Name, CreatedAt = DateTimeOffset.UtcNow });
    })
    .WithName("CreateRefund"); // POST in a nested group -> inherits the outer group's coverage

orders.MapPost("/{id}/notes", async (string id, CreateItemRequest request) =>
    {
        await Task.Delay(10);
        return TypedResults.Created(
            $"/api/orders/{id}/notes/{Guid.NewGuid()}",
            new CreateItemResponse { Id = Guid.NewGuid(), Name = request.Name, CreatedAt = DateTimeOffset.UtcNow });
    })
    .WithName("AddOrderNote"); // Mapped after the endpoints above with no declaration of its own -> still covered

// Admin group: explicit verb set covers POST and DELETE, but not PUT.
var admin = app.MapGroup("/api/admin").RequiredIdempotentKey("POST", "DELETE");

admin.MapPost("/tenants", async (CreateItemRequest request) =>
    {
        await Task.Delay(10);
        return TypedResults.Created(
            $"/api/admin/tenants/{Guid.NewGuid()}",
            new CreateItemResponse { Id = Guid.NewGuid(), Name = request.Name, CreatedAt = DateTimeOffset.UtcNow });
    })
    .WithName("CreateTenant"); // POST -> named in the verb set, covered

admin.MapDelete("/tenants/{id}", (string id) => TypedResults.Ok(new { id }))
    .WithName("DeleteTenant"); // DELETE -> named in the verb set, covered

admin.MapPut("/tenants/{id}", async (string id, CreateItemRequest request) =>
    {
        await Task.Delay(10);
        return TypedResults.Ok(new CreateItemResponse
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            CreatedAt = DateTimeOffset.UtcNow
        });
    })
    .WithName("UpdateTenant"); // PUT -> not named in the verb set, left untouched

await app.RunAsync();

namespace AspCore.Idempotency.ApiTests
{
    /// <summary>
    ///     Just for Testing purposes
    /// </summary>
    public sealed class Program;
}