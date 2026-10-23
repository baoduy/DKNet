# DKNet.AspCore.Extensions

Endpoint discovery, request binding and HTTP helpers for .NET developers building DKNet minimal APIs.

## ✨ Why use it?

- Map feature-owned route groups with consistent version and policy metadata.
- Populate host-controlled request members before validation.
- Dispatch commands or entity queries and return consistent result envelopes.
- Compose explicit middleware for host transport and request guards.

## 🚀 Quick Start

Use a .NET 10 ASP.NET Core project.

```bash
dotnet new web -f net10.0 -n PackageDemo
cd PackageDemo
dotnet add package DKNet.AspCore.Extensions
```

Replace `Program.cs` with the example, then run `dotnet run`.

```csharp
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;
using Microsoft.AspNetCore.Routing;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.UseEndpointConfigs(o =>
{
    o.EnableVersioning = false;
    o.RequireAuthorization = false;
}, typeof(PingEndpoints).Assembly);
await app.RunAsync();

public sealed class PingEndpoints : IEndpointConfig
{
    public string GroupEndpoint => "/ping";
    public void Map(RouteGroupBuilder group) => group.MapGet("/", () => "pong");
}
```

Request `GET /ping/` to see `pong`. This local example explicitly disables versioning and authorization. Normal defaults require `AddApiVersioning()` and host authentication/authorization setup.

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/index.md) | Start with architecture and the end-to-end flow. |
| [Discover and map endpoint groups](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/map-endpoint-groups.md) | Discover groups and choose versioned routes and authorization policies. |
| [Populate request members from context](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/bind-contextual-members.md) | Overwrite bound members from claims, headers, or custom host sources. |
| [Dispatch HTTP requests through SlimMessageBus](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/dispatch-bus-requests.md) | Choose command/query mappers and understand their binding and HTTP results. |
| [Read and delete entities through specifications](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/map-entity-endpoints.md) | Read, list, and hard-delete through the specification repository. |
| [Filter, search and page list endpoints](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/query-list-endpoints.md) | Validate filters, search, ordering, page sizes, and activity windows. |
| [Build a paged response envelope](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/build-paged-responses.md) | Snapshot an existing page into a response envelope. |
| [Convert FluentResults to HTTP results](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/convert-results.md) | Convert success and failure results into minimal-API responses. |
| [Apply one error-response setting](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/shape-error-responses.md) | Apply shared status and body callbacks to command, validation, and exception failures. |
| [Configure generated CRUD routes](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/configure-crud-routes.md) | Exclude generated routes and attach settings by kind or route name. |
| [Configure an explicit CORS policy](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/configure-cors.md) | Install a browser policy for explicit allowed origins. |
| [Configure trusted forwarding sources](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/trust-forwarded-headers.md) | Accept forwarded scheme and client IP only from configured sources. |
| [Configure HSTS and HTTPS redirection](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/configure-https.md) | Register HSTS settings and install HTTPS redirection. |
| [Bound request size and duration](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/bound-requests.md) | Configure Kestrel size limits and a default timeout policy. |
| [Limit rate and concurrent requests](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/limit-requests.md) | Partition local request-rate and concurrency limits. |
| [Validate antiforgery headers on browser mutations](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/guard-antiforgery.md) | Issue request-token cookies and validate headers on checked mutations. |
| [Resolve the current principal for sensitive-data serialization](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/resolve-sensitive-principal.md) | Supply the current HTTP principal to sensitive-data serialization. |
| [Select a named job from command-line arguments](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Extensions/select-jobs.md) | Match a positional argument to a known job name without executing it. |
