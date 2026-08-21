# Aspire.Hosting.ServiceBus

Hosting integration that adds the **Azure Service Bus emulator** as a resource inside a [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) AppHost. Reach for it when you want a message-driven workflow (queues/topics defined by a Service Bus emulator config file) running locally under Aspire's orchestrator — alongside the rest of your distributed application — instead of pointing every developer at a real Azure Service Bus namespace or hand-rolling a `docker run` for the emulator container.

## Install

```bash
dotnet add package Aspire.Hosting.ServiceBus
```

Reference it from your **AppHost** project only — like other Aspire hosting integrations, it is consumed by the orchestrator project, not by the services being orchestrated.

## Minimum AppHost wiring

The emulator container requires a SQL Server resource as its backing store and a JSON config file describing the emulator's namespace/queue/topic topology (the same `Config.json` format the [Azure Service Bus emulator](https://learn.microsoft.com/azure/service-bus-messaging/overview-emulator) itself expects):

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");

var serviceBus = builder.AddServiceBus(sql, configFilePath: "servicebus-config.json");

builder.AddProject<Projects.Api>("api")
    .WithReference(serviceBus);

builder.Build().Run();
```

`AddServiceBus` returns `IResourceBuilder<ServiceBusResource>`, so it composes with the rest of Aspire's fluent builder API (`WithReference`, `WithEnvironment`, `WaitFor`, etc.) exactly like any other resource builder.

## Features

### `AddServiceBus` — register the emulator resource

```csharp
public static IResourceBuilder<ServiceBusResource> AddServiceBus(
    this IDistributedApplicationBuilder builder,
    IResourceBuilder<SqlServerServerResource> sqlServer,
    string configFilePath,
    string name = "AzureBusSimulator")
```

One call wires up everything the emulator container needs:

- Adds a `ServiceBusResource` (a `ContainerResource`) running the `mcr.microsoft.com/azure-messaging/servicebus-emulator:latest` image.
- Accepts the emulator's EULA automatically (`ACCEPT_EULA=Y`) — no separate step to remember.
- Points the emulator at your SQL Server resource via the `SQL_SERVER` and `MSSQL_SA_PASSWORD` environment variables, so it uses `sqlServer`'s name and generated password automatically instead of you wiring those by hand.
- Bind-mounts `configFilePath` into the container at `/ServiceBus_Emulator/ConfigFiles/Config.json` (read-only), so your namespace/queue/topic topology lives in one versioned JSON file instead of imperative setup code.
- Exposes the emulator's two endpoints — plain AMQP on port `5672` and TLS AMQP on port `5671` — as Aspire endpoints, so other resources can discover them the normal Aspire way.
- Calls `WaitFor(sqlServer)`, so the emulator only starts once its SQL Server backing store is ready — you don't need to add that ordering yourself.
- Subscribes to Aspire's `ConnectionStringAvailableEvent` for the resource and throws a `DistributedApplicationException` if the resolved connection string ever comes back `null`, so a broken emulator setup fails fast during startup rather than surfacing as a confusing runtime connection error downstream.

`name` defaults to `"AzureBusSimulator"` if you don't need multiple emulator instances in the same AppHost.

### `ServiceBusResource` — the resource type

`ServiceBusResource` is a sealed `ContainerResource` implementing `IResourceWithConnectionString`, so it works with any Aspire API that consumes connection-string resources (`WithReference`, health checks, etc.):

- **`ConnectionStringExpression`** — builds the emulator connection string:
  `Endpoint=sb://{PrimaryEndpoint.Host};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;`
  If a `ConnectionStringRedirectAnnotation` has been applied to the resource (via Aspire's own redirection support), that redirected resource's expression is used instead — letting you swap the emulator for a real Service Bus namespace's connection string without changing anything downstream.
- **`PrimaryEndpoint`** — an `EndpointReference` to the emulator's primary (AMQP) endpoint, used to build the host in the connection string above and available if you need to reference it directly.
- **`GetConnectionStringAsync(CancellationToken)`** — resolves the connection string asynchronously, honoring the same redirect annotation as `ConnectionStringExpression`.

## Configuration options and defaults

| Parameter | Required | Default | Purpose |
|---|---|---|---|
| `sqlServer` | yes | — | The SQL Server resource the emulator uses as its backing store; the emulator waits on it before starting. |
| `configFilePath` | yes | — | Host path to the emulator's `Config.json` (namespaces, queues, topics, subscriptions) — bind-mounted read-only into the container. |
| `name` | no | `"AzureBusSimulator"` | The Aspire resource name for the emulator. |

There is no fluent API on `ServiceBusResource` for declaring queues or topics in code — that topology is entirely the responsibility of the JSON file passed as `configFilePath`. Image, registry, and tag (`mcr.microsoft.com/azure-messaging/servicebus-emulator:latest`) and both endpoint ports (`5672`, `5671`) are fixed by the extension and are not currently configurable through parameters.

## Composing with DKNet.SlimBus.Extensions

Aspire only provisions the transport; message handling still runs through [`DKNet.SlimBus.Extensions`](../Messaging/DKNet.SlimBus.Extensions.md). Once you've wired `WithReference(serviceBus)` onto a downstream project, that project resolves the connection string the normal Aspire configuration way and can hand it to `WithProviderServiceBus(cfg => cfg.ConnectionString = ...)` when configuring SlimMessageBus — using the emulator locally and a real Azure Service Bus connection string in higher environments, with no change to handler code.

## Gotchas and limits

- **Emulator only.** The extension always provisions the `azure-messaging/servicebus-emulator` container — there is no built-in path to have it provision or reference a real Azure Service Bus namespace. Point production configuration at a real connection string outside this package (or use `ConnectionStringRedirectAnnotation` to redirect `ServiceBusResource` to another resource's connection string).
- **SQL Server dependency is mandatory.** The Service Bus emulator image itself requires a SQL Server backing store; `AddServiceBus` always requires a `SqlServer` resource and always waits on it — there's no way to run the emulator without one.
- **Topology lives outside the fluent API.** Queues, topics, and subscriptions are defined in the emulator's `Config.json`, not through builder methods — if you're looking for `WithQueue`/`WithTopic`-style calls, they don't exist in this package.
- **No health-check wiring.** `AddServiceBus` does not register an Aspire health check for the resource; add one yourself if you need readiness gating before dependent services start.
- **Fixed ports and image tag.** The emulator always runs the `latest` tag on ports `5672`/`5671`; there's currently no parameter to pin a specific image version or remap ports.
