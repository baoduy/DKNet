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

builder.Services.AddIdempotentKey();
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

await app.RunAsync();

namespace AspCore.Idempotency.ApiTests
{
    /// <summary>
    ///     Just for Testing purposes
    /// </summary>
    public sealed class Program;
}