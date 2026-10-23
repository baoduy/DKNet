# Configure an explicit CORS policy

Part of [DKNet.AspCore.Extensions](index.md). `AddCorsConfig` reads allowed origins and installs a default policy only when at least one nonblank origin is configured. `UseCorsConfig` calls `UseCors` only if registration set its feature marker.

```csharp
using DKNet.AspCore.Extensions.Hosting;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCorsConfig(builder.Configuration);
var app = builder.Build();
app.UseCorsConfig();
Console.WriteLine("CorsConfig pipeline configured.");
```

![Configure an explicit CORS policy: the host registers settings and the matching Use method installs middleware only when its feature marker exists.](diagrams/configure-cors.svg)

## ⚙️ Configuration reference

| Configuration key | Type | Default | Effect |
|---|---|---|---|
| `Cors:AllowedOrigins` | `string[]` | empty | No policy or marker when empty after dropping blank entries. |
| `Cors:AllowedMethods` | `string[]` | GET, POST, PUT, PATCH | Methods on the default policy. |
| `Cors:AllowedHeaders` | `string[]` | Authorization, Content-Type, Accept, X-Idempotency-Key | Headers on the default policy. |

## ⚠️ Gotchas & limits

- DELETE is not in the default allowed methods. Configure it if the browser client needs it.
- This helper does not enable credentials or wildcard origins. An explicitly supplied empty methods/headers array replaces the default array.
- Call the Use helper in the host’s intended middleware order. The registration helper alone serves no CORS policy.
