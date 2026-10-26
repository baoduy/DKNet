# Populate request members from context

Part of [DKNet.AspCore.Extensions](index.md). Register `AddContextualRequestPopulation()` before building the host. On groups discovered by `UseEndpointConfigs`, declarations overwrite bound values before later group filters and the handler.

```csharp
using System.Security.Claims;
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.ModelBinding;
using Microsoft.AspNetCore.Routing;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddContextualRequestPopulation();
var app = builder.Build();
app.UseEndpointConfigs(o => { o.EnableVersioning = false; o.RequireAuthorization = false; },
    typeof(HeaderEndpoints).Assembly);
await app.RunAsync();

public sealed record HeaderRequest
{
    [FromClaim(ClaimTypes.NameIdentifier)]
    public string? CreatedBy { get; set; }

    [FromRequestHeader("X-Correlation-Id")]
    public string? CorrelationId { get; set; }
}
public sealed class HeaderEndpoints : IEndpointConfig
{
    public string GroupEndpoint => "/headers";
    public void Map(RouteGroupBuilder group) => group.MapPost("/", (HeaderRequest input) => input);
}
```

![A discovered group scans contextual declarations, resolves the first matching source and converts or defaults each property before later filters.](diagrams/contextual-members.svg)

POST `{}` with `X-Correlation-Id: demo-1` to receive the header value in `CorrelationId`. `CreatedBy` receives the first `ClaimTypes.NameIdentifier` claim from the current principal; without that claim it becomes null, even if the body supplied a value. The example disables authorization for a local demonstration; an application's authentication and authorization remain host responsibilities. Both attributes target a property once, without inheritance; their constructors take one non-null source name. Header lookup uses the first value and case-insensitive header names.

`IContextualSource` marks custom attributes. Implement it on a property attribute, then register a scoped `IContextualValueResolver` alongside the population mechanism. Its `bool CanResolve(IContextualSource source)` selects supported declarations; `string? Resolve(IContextualSource source, HttpContext httpContext)` supplies the raw value. The first matching resolver wins, including a null result; a custom resolver registered before built-ins replaces their behavior for the source types it accepts.

The population service overwrites unresolved values with the destination type’s default. Non-string conversion uses invariant `TypeDescriptor` conversion; format, unsupported-conversion and argument failures produce defaults. The service and built-in resolvers are scoped, with shared metadata/converter caches.

OpenAPI transformers hide declared members from body schemas and query parameters. Header declarations add a deduplicated, optional string header parameter; claim declarations stay hidden. Transformers are configured for every OpenAPI document the host registers.

## ⚠️ Gotchas & limits

- Declarations on a manually mapped endpoint do not add population by themselves. Use a discovered group.
- Missing registration or a declared property without a setter fails endpoint construction. Both `set` and `init` are accepted.
- Missing headers and failed conversion do not reject requests. Add validation when a value is required.
- Caller-supplied headers are binding inputs, not authenticated identity.
- A shared custom fallback value can collapse otherwise separate caller identities or idempotency keys.
