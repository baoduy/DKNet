# Configure HSTS and HTTPS redirection

Part of [DKNet.AspCore.Extensions](index.md). `AddHttpsConfig` registers HSTS options and the feature marker. `UseHttpsConfig` installs HSTS followed by HTTPS redirection when marked. It supplies no TLS certificate or HTTPS listener.

```csharp
using DKNet.AspCore.Extensions.Hosting;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpsConfig(builder.Configuration);
var app = builder.Build();
app.UseHttpsConfig();
Console.WriteLine("HttpsConfig pipeline configured.");
```

![Configure HSTS and HTTPS redirection: the host registers settings and the matching Use method installs middleware only when its feature marker exists.](diagrams/configure-https.svg)

## ⚙️ Configuration reference

| Input | Type | Default | Effect |
|---|---|---|---|
| `Https:HstsMaxAgeDays` | number | 365 | Default HSTS MaxAge; includes subdomains and enables preload at 365 days or more. |
| `configureOptions` | `Action<HstsOptions>?` | null | Replaces the helper’s HSTS defaults with the supplied framework options callback. |

## ⚠️ Gotchas & limits

- The callback branch does not apply the helper's IncludeSubDomains or preload calculation.
- Behind a proxy, configure forwarded scheme handling before redirection.
- The helper calls HSTS whenever marked; it contains no Development-environment condition of its own.
