# Register the Service Bus emulator resource

Part of [Aspire.Hosting.ServiceBus](index.md). `AddServiceBus` adds a `ServiceBusResource` to the AppHost and wires everything the emulator container needs in one call.

```csharp
using Aspire.Hosting;
using Aspire.Hosting.ServiceBus;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");
var serviceBus = builder.AddServiceBus(sql, configFilePath: "servicebus-config.json");

builder.AddProject("api", "../Api/Api.csproj")
    .WithReference(serviceBus);

builder.Build().Run();
```

`AddServiceBus` returns `IResourceBuilder<ServiceBusResource>`, so it composes with the rest of Aspire's fluent builder API (`WithReference`, `WithEnvironment`, `WaitFor`, …) exactly like any other resource builder. The `name` parameter defaults to `"AzureBusSimulator"` when omitted.

![sqlServer, configFilePath and name feed AddResource, which sets the emulator image, its ACCEPT_EULA/SQL_SERVER/MSSQL_SA_PASSWORD environment variables, a read-only Config.json bind mount, then the tcp/tcp2 endpoints and WaitFor(sqlServer).](diagrams/register-emulator-resource.svg)

The config file passed as `configFilePath` is the same `Config.json` format the [Azure Service Bus emulator](https://learn.microsoft.com/azure/service-bus-messaging/overview-emulator) itself expects — namespaces, queues, topics and subscriptions all live in that one versioned file; there is no fluent API on the resource for declaring them in code.

## ⚙️ Configuration reference

| Parameter | Required | Default | Effect |
|---|---|---|---|
| `sqlServer` | yes | — | The SQL Server resource the emulator uses as its backing store. Its `Name` and `PasswordParameter` become the `SQL_SERVER` and `MSSQL_SA_PASSWORD` environment variables, and the emulator waits on it via `WaitFor`. |
| `configFilePath` | yes | — | Host path to the emulator's `Config.json`, bind-mounted read-only at `/ServiceBus_Emulator/ConfigFiles/Config.json`. |
| `name` | no | `"AzureBusSimulator"` | The Aspire resource name. |

Fixed by the extension and not configurable through parameters: the image (`mcr.microsoft.com/azure-messaging/servicebus-emulator:latest`), `ACCEPT_EULA=Y`, and both AMQP endpoints (`tcp` on port `5672`, `tcp2` on port `5671`).

## ⚠️ Gotchas & limits

- **SQL Server is mandatory.** The emulator image itself requires a SQL Server backing store; `AddServiceBus` always requires a `SqlServer` resource and always waits on it — there is no way to run the emulator without one.
- **Topology lives outside the fluent API.** Queues, topics and subscriptions come entirely from `Config.json`; there are no `WithQueue`/`WithTopic`-style builder methods on `ServiceBusResource`.
- **Fixed image tag and ports.** The emulator always runs the `latest` tag on host ports `5672`/`5671` — there is no parameter to pin a specific image version or remap ports, so two emulator instances in one AppHost would collide on those ports.
- **No health check is registered.** The csproj references `Microsoft.Extensions.Diagnostics.HealthChecks`, but `AddServiceBus` does not add a health check for the resource — add one yourself if you need readiness gating before dependent services start.
