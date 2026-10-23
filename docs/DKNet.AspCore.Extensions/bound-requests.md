# Bound request size and duration

Part of [DKNet.AspCore.Extensions](index.md). `AddRequestBoundsConfig` reads one `RequestBoundsOptions` object, configures Kestrel limits and removes its server header. It also registers a default request-timeout policy returning 504. `UseRequestBoundsConfig` installs timeout middleware only when marked.

```csharp
using DKNet.AspCore.Extensions.Hosting;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRequestBoundsConfig(builder.Configuration);
var app = builder.Build();
app.UseRequestBoundsConfig();
Console.WriteLine("RequestBoundsConfig pipeline configured.");
```

![Bound request size and duration: the host registers settings and the matching Use method installs middleware only when its feature marker exists.](diagrams/bound-requests.svg)

## ⚙️ Configuration reference

| `RequestBounds` key | Type | Default | Effect |
|---|---|---|---|
| `RequestTimeoutSeconds` | `int` | 30 | Default timeout duration, with 504 timeout status. |
| `MaxRequestBodySizeBytes` | `long` | 1048576 | Kestrel maximum body size. |
| `RequestHeadersTimeoutSeconds` | `int` | 10 | Kestrel header-read timeout. |

## ⚠️ Gotchas & limits

- These body/header limits configure Kestrel, not an upstream proxy. Other servers have their own limits.
- Values are read during registration; the helper adds no options validation or live refresh.
- Request code must observe cancellation for prompt timeout handling; a timeout is not business-operation rollback.
