# Supply your own rate-limit provider

Part of [DKNet.AspCore.Extensions](index.md). [Limit rate and concurrent requests](limit-requests.md) uses `IRateLimitOptionsProvider` to decide the fixed-window and concurrency limits for the caller of a request. Implement it to vary limits per caller instead of applying the same `RateLimitOptions` to everyone.

```csharp
using DKNet.AspCore.Extensions.RateLimits;
using Microsoft.AspNetCore.Http;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IRateLimitOptionsProvider, PremiumAwareRateLimitOptionsProvider>();
builder.Services.AddRateLimitConfig(builder.Configuration);
var app = builder.Build();
app.UseRateLimitConfig();
Console.WriteLine("Custom IRateLimitOptionsProvider registered; a signed-in premium caller gets a higher limit.");

public sealed class PremiumAwareRateLimitOptionsProvider : IRateLimitOptionsProvider
{
    public FixedWindowRateLimiterOptions GetRateLimiterOptions(HttpContext httpContext) => new()
    {
        PermitLimit = httpContext.User.IsInRole("premium") ? 100 : 2,
        QueueLimit = 0,
        Window = TimeSpan.FromSeconds(1)
    };

    public ConcurrencyLimiterOptions GetConcurrencyLimiterOptions(HttpContext httpContext) => new()
    {
        PermitLimit = httpContext.User.IsInRole("premium") ? 50 : 2,
        QueueLimit = 0
    };
}
```

![Workflow diagram of supplying a custom IRateLimitOptionsProvider: you implement the two methods, register the type with AddSingleton in either order relative to AddRateLimitConfig, then the global limiter resolves it when it first builds the fixed-window and concurrency limiter for a partition key.](diagrams/customise-rate-limits.svg)

Register your implementation with `services.AddSingleton<IRateLimitOptionsProvider, TYourProvider>()`. `AddRateLimitConfig` registers its own default with `TryAddSingleton`, so your registration wins whether it runs before or after `AddRateLimitConfig` — before, because `TryAddSingleton` then finds a registration already present and skips; after, because the last registration for a service type wins when more than one is present. The example reads `httpContext.User.IsInRole("premium")` because [the partition key](limit-requests.md) is `user:<Identity.Name>` for a signed-in caller, so each user gets their own limiter built from their own role; a value read from a header or query string would not vary this reliably, since unauthenticated callers behind the same address share one `ip:` partition and its one limiter.

## ⚙️ Configuration reference

| `IRateLimitOptionsProvider` member | Returns | Called with |
|---|---|---|
| `GetRateLimiterOptions(HttpContext)` | `FixedWindowRateLimiterOptions` | The request being limited. |
| `GetConcurrencyLimiterOptions(HttpContext)` | `ConcurrencyLimiterOptions` | The request being limited. |

There is no options class to configure here: implementing this interface *is* the override point. The default implementation reads `IOptions<RateLimitOptions>` — see [Limit rate and concurrent requests](limit-requests.md) for that options reference — and returns the same two values for every caller.

## 🧱 Where it fits

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| [`AddRateLimitConfig`'s global limiter](limit-requests.md) | DKNet.AspCore.Extensions (same package) | Resolves the registered `IRateLimitOptionsProvider` from `HttpContext.RequestServices` and calls both methods when it builds a new fixed-window or concurrency limiter for a partition key. |

None outside this package — no other DKNet package implements or resolves `IRateLimitOptionsProvider`.

**Entry point in your app:** `builder.Services.AddSingleton<IRateLimitOptionsProvider, TYourProvider>()` in `Program.cs`.

## ⚠️ Gotchas & limits

- Both methods run inside the limiter-creation factory for a partition key, not on every request for that key — keep them fast and allocation-light regardless, since the very first request for a new key still pays their cost inline.
- A limiter is built once per partition key and then reused until that key's limiter goes idle and is recreated. Changing what your provider would return later (a role change, a header value) does not retroactively resize a caller's current limiter. Two callers who share a key — for example two anonymous callers behind the same address — share that one limiter and whatever options built it.
- A provider that throws is not caught by this package; an unhandled exception from either method fails the request instead of falling back to the default limits.
