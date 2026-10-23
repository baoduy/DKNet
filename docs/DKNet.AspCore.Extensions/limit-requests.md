# Limit rate and concurrent requests

Part of [DKNet.AspCore.Extensions](index.md). `AddRateLimitConfig` chains a fixed-window limiter and a concurrency limiter as the global limiter, with 429 rejection and no queue. The partition key is `user:` plus `User.Identity.Name` if nonempty, else `ip:` plus remote address, else `shared`.

The default singleton `IRateLimitOptionsProvider` reads `IOptions<RateLimitOptions>` and supplies both limiter configurations. Register your own provider before this helper to replace its `TryAddSingleton` default. The interface receives `HttpContext` and exposes `GetRateLimiterOptions` and `GetConcurrencyLimiterOptions`.

```csharp
using DKNet.AspCore.Extensions.RateLimits;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRateLimitConfig(builder.Configuration);
var app = builder.Build();
app.UseRateLimitConfig();
Console.WriteLine("RateLimitConfig pipeline configured.");
```

![Limit rate and concurrent requests: the host registers settings and the matching Use method installs middleware only when its feature marker exists.](diagrams/limit-requests.svg)

## ⚙️ Configuration reference

| `RateLimit` key | Type | Default | Effect |
|---|---|---|---|
| `DefaultRequestLimit` | `int` | 2 | Fixed-window permits per partition. |
| `DefaultConcurrentLimit` | `int` | 2 | Concurrent permits per partition. |
| `TimeWindowInSeconds` | `int` | 1 | Fixed-window duration. |

Both default limiter option objects set `QueueLimit = 0`.

## ⚠️ Gotchas & limits

- Partitions are local to the host process; this is not a distributed rate ledger.
- The key reads Identity.Name without checking IsAuthenticated. Host identity construction and middleware order matter.
- Put authentication before limiting when a populated principal should determine the key, and trusted forwarding before IP partitioning.
- The helper binds options but adds no startup validators. A replacement provider is responsible for valid limiter settings.
