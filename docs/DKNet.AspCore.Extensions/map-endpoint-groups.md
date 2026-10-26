# Discover and map endpoint groups

Part of [DKNet.AspCore.Extensions](index.md). Implement `IEndpointConfig` with a public parameterless constructor. `GroupEndpoint` and `Map(RouteGroupBuilder)` are required; `Version` defaults to 1 and `Tag` derives from the group segment by replacing slashes with hyphens and trimming leading hyphens.

```csharp
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;
using Microsoft.AspNetCore.Routing;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthorization();
builder.Services.AddApiVersioning();
var app = builder.Build();
app.UseEndpointConfigs(assemblies: [typeof(PingEndpoints).Assembly]);
Console.WriteLine("Mapped /v{version:apiVersion}/ping.");

public sealed class PingEndpoints : IEndpointConfig
{
    public string GroupEndpoint => "/ping";
    public void Map(RouteGroupBuilder group) => group.MapGet("/", () => "pong");
}
```

![Group scope metadata chooses route overrides first, then method declarations, then the group default, refusing uncovered methods.](diagrams/group-policies.svg)

`UseEndpointConfigs` scans nonabstract, nongeneric implementations, creates one group each, attaches tags and version metadata, then the contextual population filter, host `ConfigureGroup`, authorization and finally the config’s `Map`. With no assemblies specified, it scans currently loaded assemblies. No discovered config returns an empty list.

`[EndpointGroupScope("accounts.read")]` declares a group-default policy name; the form with `EndpointHttpMethods.Post`, `Put`, `Patch`, `Delete`, `Get`, `Head` or `Options` declares method-specific names. The host must register the corresponding authorization policies. Route-specific named `RequireAuthorization(...)` or `AllowAnonymous()` wins, then method declaration, then default. A declared group with an uncovered served method fails endpoint construction. A verb-less endpoint needs a default or route-level rule. A multi-verb endpoint receives every distinct policy selected for its served verbs.

`IEndpointConfig.AuthPolicy` is removed. Replace a former group-wide policy with the scope-only attribute. Groups with no attributes keep plain authorization.

## ⚙️ Configuration reference

| Option | Type | Default | Effect |
|---|---|---|---|
| `RouteTemplate` | `Func<IEndpointConfig, string>?` | null | Uses `/v{version:apiVersion}{GroupEndpoint}`, or `GroupEndpoint` without versioning. |
| `DefaultTag` | `string` | `Root` | Used for an empty config tag. |
| `RequireAuthorization` | `bool` | true | Adds authorization and enables group-scope declarations. |
| `EnableVersioning` | `bool` | true | Requires `AddApiVersioning()` even when discovery finds no configs. |
| `ConfigureGroup` | `Action<RouteGroupBuilder, IEndpointConfig>?` | null | Host setup after population filter registration and before authorization and `Map`. |
| `assemblies` | `params Assembly[]` | loaded assemblies | Limits discovery when supplied. |

## ⚠️ Gotchas & limits

- Config objects are constructed through `Activator.CreateInstance`, not DI. Inject dependencies into handlers instead.
- Disabling `RequireAuthorization` also disables scope discovery and uncovered-method refusal. Authentication remains the host's responsibility.
- Scope strings become authorization policy names; the helper does not itself inspect OAuth scope claims.
- Request-time authorization middleware runs before endpoint filters. Population is first among the group’s endpoint filters, not before authorization middleware.
