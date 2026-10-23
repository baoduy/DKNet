# Validate antiforgery headers on browser mutations

Part of [DKNet.AspCore.Extensions](index.md). Register the guard, then place its middleware after routing and any host authentication/authorization middleware it relies on:

```csharp
using DKNet.AspCore.Extensions.Antiforgery;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAntiforgeryConfig();
var app = builder.Build();
app.UseRouting();
app.UseAntiforgeryConfig();
app.MapGet("/", () => "token cookie issued");
app.MapPost("/", () => Results.Ok());
Console.WriteLine("Antiforgery guard configured; serve over HTTPS.");
```

![GET issues token cookies; covered mutations validate a header and token unless endpoint metadata or a Bearer-shaped header bypasses the check.](diagrams/antiforgery-guard.svg)

On GET, the guard calls `GetAndStoreTokens` and writes a readable request-token cookie. POST, PUT, PATCH and DELETE require the configured request header and a valid antiforgery token unless the endpoint disables validation or the Authorization header has exactly one nonblank Bearer value. The guard does not authenticate that Bearer token; the host must do so.

A refused request returns fixed 400 `application/problem+json`, with an `Antiforgery` error code and trace ID. It does not read the host’s `ErrorResponseOptions`. Accepted/bypassed requests receive a valid `IAntiforgeryValidationFeature` before the next middleware runs.

## ⚙️ Configuration reference

| `AntiforgeryGuardOptions` option | Type | Default | Effect |
|---|---|---|---|
| `TokenCookieName` | `string` | `XSRF-TOKEN` | Readable request-token cookie, Secure, SameSite Strict, Path `/`. |
| `HeaderName` | `string` | `X-XSRF-TOKEN` | Required header for checked mutations. |
| `SecretCookieName` | `string?` | null | Optional framework secret-cookie name. |

The secret cookie is HttpOnly, Secure Always and SameSite Strict. Configure options with `AddAntiforgeryConfig(Action<AntiforgeryGuardOptions>?)`.

## ⚠️ Gotchas & limits

- This is a header-based guard, not a body-token fallback. A checked mutation without the configured header is refused.
- A Bearer-shaped header bypass is not proof of authorization. Configure host authentication and authorization independently.
- Secure cookies require HTTPS in clients. HEAD does not issue a new token cookie; GET does.
- `UseAntiforgeryConfig` is inert without its registration marker. It installs the framework antiforgery middleware before the guard.
