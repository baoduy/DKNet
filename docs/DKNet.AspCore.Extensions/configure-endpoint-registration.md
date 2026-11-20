# Configure endpoint registration

Part of [DKNet.AspCore.Extensions](index.md). `EndpointRegistrationOptions` is the object `UseEndpointConfigs`'s `configureOptions` callback receives. Every default reproduces the behaviour you get by passing no callback at all.

```csharp
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;
using Microsoft.AspNetCore.Routing;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthorization();
var app = builder.Build();
app.UseEndpointConfigs(o =>
{
    o.EnableVersioning = false;
    o.DefaultTag = "Demo";
    o.RouteTemplate = config => $"/api{config.GroupEndpoint}";
    o.ConfigureGroup = (group, config) => group.WithSummary("Demo group");
}, typeof(PingEndpoints).Assembly);
Console.WriteLine("Mapped /api/ping, tagged Demo, with a summary added by ConfigureGroup.");

public sealed class PingEndpoints : IEndpointConfig
{
    public string GroupEndpoint => "/ping";
    public void Map(RouteGroupBuilder group) => group.MapGet("/", () => "pong");
}
```

![Workflow diagram of UseEndpointConfigs applying EndpointRegistrationOptions: defaults are built and your callback runs once, the EnableVersioning guard throws when no AddApiVersioning() is registered, then per discovered group the RouteTemplate/DefaultTag choice runs before ConfigureGroup and the RequireAuthorization branch.](diagrams/endpoint-registration-options.svg)

This is the same options object used by [Discover and map endpoint groups](map-endpoint-groups.md) — that page covers what you implement per `IEndpointConfig`; this page is the full reference for the options object itself. There is no `AddEndpointRegistrationOptions`; you pass the callback directly to `UseEndpointConfigs`, and it runs once per call, before the `EnableVersioning` guard.

## ⚙️ Configuration reference

| `EndpointRegistrationOptions` option | Type | Default | Effect |
|---|---|---|---|
| `RouteTemplate` | `Func<IEndpointConfig, string>?` | `null` | Builds each group's route pattern. `null` uses `/v{version:apiVersion}{GroupEndpoint}` when `EnableVersioning` is `true`, or plain `{GroupEndpoint}` when it is `false`. |
| `DefaultTag` | `string` | `"Root"` | Applied when a config's `IEndpointConfig.Tag` is empty. |
| `RequireAuthorization` | `bool` | `true` | Adds `RequireAuthorization()` to every group and enables `[EndpointGroupScope]` declarations. Setting it `false` is an explicit per-host opt-out. |
| `EnableVersioning` | `bool` | `true` | Requires `AddApiVersioning()` to be registered; `UseEndpointConfigs` throws `InvalidOperationException` otherwise, even with zero discovered configs. |
| `ConfigureGroup` | `Action<RouteGroupBuilder, IEndpointConfig>?` | `null` | Runs for every created group, after mapping/version metadata/tags are applied and before authorization and `IEndpointConfig.Map` run. Use it for host-wide setup such as request-user stamping or a validation filter. |

There is no interface to implement or type to register here: `EndpointRegistrationOptions` is a plain options object you mutate inside the `configureOptions` delegate passed to `UseEndpointConfigs`. `ConfigureGroup` is itself the override point for per-group host setup that this package does not build in.

## 🧱 Where it fits

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| [`UseEndpointConfigs`](map-endpoint-groups.md) | DKNet.AspCore.Extensions (same package) | Constructs `EndpointRegistrationOptions`, invokes your `configureOptions` callback once, then reads every member while discovering and building each route group. |

None outside this package — no other DKNet package calls `UseEndpointConfigs` or configures `EndpointRegistrationOptions`.

**Entry point in your app:** the `configureOptions` delegate passed to `app.UseEndpointConfigs(...)` in `Program.cs`.

## ⚠️ Gotchas & limits

- `configureOptions` runs exactly once per `UseEndpointConfigs` call, before discovery and before the `EnableVersioning` guard — a callback that reads `app.Services` for conditional logic still runs even when discovery finds zero `IEndpointConfig` implementations.
- Setting `EnableVersioning` to `false` does not remove existing `AddApiVersioning()` registration. It skips the startup guard, the version-aware route template, and the `ApiVersionSet`/`WithApiVersionSet`/`HasApiVersion`/`MapToApiVersion` metadata every group would otherwise get.
- `RouteTemplate` replaces the whole per-group template, including the version segment. Reintroduce `{version:apiVersion}` yourself if you still need versioned routes with a custom path shape.
- `ConfigureGroup` runs for every discovered group with no way to opt a single group out; branch on `config.GetType()` or `config.GroupEndpoint` inside the callback if only some groups need the setup.
