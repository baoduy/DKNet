# Resolve the current principal for sensitive-data serialization

Part of [DKNet.AspCore.Extensions](index.md). `AddSensitiveDataPrincipalAccessor` connects the EF Core sensitive-data serialization contract to the current HTTP user:

```csharp
using DKNet.AspCore.Extensions.Security;
using DKNet.EfCore.Extensions.Serialization;
using Microsoft.Extensions.DependencyInjection;
var services = new ServiceCollection();
services.AddSensitiveDataPrincipalAccessor();
using var provider = services.BuildServiceProvider();
Console.WriteLine(provider.GetRequiredService<ISensitiveDataPrincipalAccessor>().Current is null); // True
```

![The singleton principal accessor reads the ambient HttpContext user each time and returns null outside a request.](diagrams/sensitive-principal.svg)

The helper adds `IHttpContextAccessor` and tries to register the internal singleton `HttpContextSensitiveDataPrincipalAccessor` for `ISensitiveDataPrincipalAccessor`. Its `Current` reads `HttpContext.User` on access; outside a request it returns null. An existing contract registration is retained.

## ⚠️ Gotchas & limits

- This registration neither authenticates a user nor configures sensitive-data serialization rules. It only supplies the principal bridge.
- An existing custom `ISensitiveDataPrincipalAccessor` takes precedence because registration uses `TryAddSingleton`.
- Do not assume an HTTP principal exists in startup jobs or other work outside the request context.
