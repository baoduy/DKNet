# Configure trusted forwarding sources

Part of [DKNet.AspCore.Extensions](index.md). `AddForwardedHeadersConfig` parses explicit trusted IP addresses and CIDR networks. It clears the framework’s known-proxy/network collections. Without trusted entries, forwarding is disabled. With any entries, it enables X-Forwarded-For and X-Forwarded-Proto for those sources.

```csharp
using DKNet.AspCore.Extensions.Hosting;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddForwardedHeadersConfig(builder.Configuration);
var app = builder.Build();
app.UseForwardedHeadersConfig();
Console.WriteLine("ForwardedHeadersConfig pipeline configured.");
```

![Configure trusted forwarding sources: the host registers settings and the matching Use method installs middleware only when its feature marker exists.](diagrams/trust-forwarded-headers.svg)

## ⚙️ Configuration reference

| Configuration key | Type | Default | Effect |
|---|---|---|---|
| `Security:TrustedProxies` | `string[]` | empty | IPs parsed with `IPAddress.Parse`. |
| `Security:TrustedNetworks` | `string[]` | empty | CIDRs parsed with `System.Net.IPNetwork.Parse`. |

## ⚠️ Gotchas & limits

- Invalid addresses or networks throw during registration.
- This helper does not enable forwarded host processing. Unlisted sources do not become trusted merely by sending headers.
- Place forwarding before middleware that consumes scheme or client IP, such as HTTPS redirection and partition selection.
