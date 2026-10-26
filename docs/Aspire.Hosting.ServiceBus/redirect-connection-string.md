# Redirect the connection string to a real namespace

Part of [Aspire.Hosting.ServiceBus](index.md). `ServiceBusResource` implements `IResourceWithConnectionString`, and both of its connection-string members check for a `ConnectionStringRedirectAnnotation` before falling back to the emulator's own value — so a referencing project can be pointed at a real Azure Service Bus namespace without changing its own code.

```csharp
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ServiceBus;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");
var serviceBus = builder.AddServiceBus(sql, configFilePath: "servicebus-config.json");

if (builder.ExecutionContext.IsPublishMode)
{
    var realNamespace = builder.AddConnectionString("real-service-bus");
    serviceBus.Resource.Annotations.Add(new ConnectionStringRedirectAnnotation(realNamespace.Resource));
}

builder.AddProject("api", "../Api/Api.csproj")
    .WithReference(serviceBus);

builder.Build().Run();
```

`ConnectionStringExpression` and `GetConnectionStringAsync(CancellationToken)` both call `TryGetLastAnnotation<ConnectionStringRedirectAnnotation>` first; when an annotation is present they return the annotated resource's own expression or resolved value instead of building `Endpoint=sb://{PrimaryEndpoint.Host};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;`.

![ConnectionStringExpression and GetConnectionStringAsync both check TryGetLastAnnotation for a ConnectionStringRedirectAnnotation; without one they build the emulator's UseDevelopmentEmulator connection string, with one they return the annotated resource's own expression or resolved value instead.](diagrams/redirect-connection-string.svg)

## 🧱 Where it fits

The redirect only changes which string a referencing project receives through `WithReference`. `AddServiceBus` still provisions the full emulator resource — the container, the SQL Server dependency and `WaitFor`, the config bind mount — regardless of whether a redirect is applied; nothing short-circuits that wiring. See [index.md](index.md) for what else `AddServiceBus` adds alongside the resource this annotation targets.

## ⚠️ Gotchas & limits

- **The emulator is still provisioned.** Applying a redirect does not skip adding the emulator container, its SQL Server dependency, or the config bind mount — only the two connection-string members change what they return.
- **`PrimaryEndpoint` is never redirected.** It always resolves to the emulator's own endpoint, even when `ConnectionStringExpression` points elsewhere.
- **The annotation is read on every call.** There is no caching of the redirect decision — `TryGetLastAnnotation` runs fresh each time `ConnectionStringExpression` or `GetConnectionStringAsync` is evaluated, so adding or removing the annotation changes the result from that point on.
- **No built-in Azure Service Bus resource type.** This package provisions the emulator only; wiring a `ConnectionStringRedirectAnnotation` to a real namespace is left entirely to the host application — there is no first-party "real Service Bus" resource type here to redirect to.
