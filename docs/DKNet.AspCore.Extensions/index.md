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

## 🏗️ Architecture

![The consumer host chooses endpoint groups and mapper families, contextual resolvers, response conversion and explicit middleware guards.](diagrams/components.svg)

These helpers run inside the consumer’s ASP.NET Core process. Contextual resolvers are scoped; the host supplies authentication, authorization policies, bus handlers, repository configuration and external databases. Middleware helpers use keyed registration markers; `MarkFeatureAdded` and `IsFeatureAdded` are public marker utilities, not middleware execution.

## 🔄 How it works

![UseEndpointConfigs checks versioning, discovers configs and builds each group with population, host filters, authorization and routes.](diagrams/aspcore-extensions-endpoint-mapping.svg)

Versioning is enabled by default and checked before discovery. The Quick Start opts out explicitly. A declared contextual member requires population registration and a setter; missing setup fails when endpoint metadata is built.

## 🧩 Features

- [Discover and map endpoint groups](map-endpoint-groups.md) — Discover groups and choose versioned routes and authorization policies.
- [Populate request members from context](bind-contextual-members.md) — Overwrite bound members from claims, headers, or custom host sources.
- [Dispatch HTTP requests through SlimMessageBus](dispatch-bus-requests.md) — Choose command/query mappers and understand their binding and HTTP results.
- [Read and delete entities through specifications](map-entity-endpoints.md) — Read, list, and hard-delete through the specification repository.
- [Filter, search and page list endpoints](query-list-endpoints.md) — Validate filters, search, ordering, page sizes, and activity windows.
- [Build a paged response envelope](build-paged-responses.md) — Snapshot an existing page into a response envelope.
- [Convert FluentResults to HTTP results](convert-results.md) — Convert success and failure results into minimal-API responses.
- [Apply one error-response setting](shape-error-responses.md) — Apply shared status and body callbacks to command, validation, and exception failures.
- [Configure generated CRUD routes](configure-crud-routes.md) — Exclude generated routes and attach settings by kind or route name.
- [Configure an explicit CORS policy](configure-cors.md) — Install a browser policy for explicit allowed origins.
- [Configure trusted forwarding sources](trust-forwarded-headers.md) — Accept forwarded scheme and client IP only from configured sources.
- [Configure HSTS and HTTPS redirection](configure-https.md) — Register HSTS settings and install HTTPS redirection.
- [Bound request size and duration](bound-requests.md) — Configure Kestrel size limits and a default timeout policy.
- [Limit rate and concurrent requests](limit-requests.md) — Partition local request-rate and concurrency limits.
- [Validate antiforgery headers on browser mutations](guard-antiforgery.md) — Issue request-token cookies and validate headers on checked mutations.
- [Resolve the current principal for sensitive-data serialization](resolve-sensitive-principal.md) — Supply the current HTTP principal to sensitive-data serialization.
- [Select a named job from command-line arguments](select-jobs.md) — Match a positional argument to a known job name without executing it.

## 🔗 Related packages

- [Idempotency](../DKNet.AspCore.Idempotency/index.md) — reserve keys for protected request retries.
- [Tasks](../DKNet.AspCore.Tasks/index.md) — run a background batch at host start.
- [SlimBus Extensions](../Messaging/DKNet.SlimBus.Extensions.md) — configure command/query dispatch.
- [SlimBus Generators](../Messaging/DKNet.SlimBus.Generators.md) — generate CRUD request and mapping code.
- [EF Core Specifications](../EfCore/DKNet.EfCore.Specifications.md) — configure repository reads and saves.
- [Fw Extensions](../DKNet.Fw.Extensions/index.md) — inspect types and service registrations.
