# Aspire.Hosting.ServiceBus

Hosting integration that adds the Azure Service Bus **emulator** as a resource inside a .NET Aspire AppHost, for developers wiring a message-driven workflow into local orchestration.

## ✨ Why use it?

- **No shared cloud namespace for local work.** Every developer's AppHost spins up its own emulator instead of pointing at a real Azure Service Bus namespace.
- **No hand-rolled container setup.** One call wires the image, the EULA acceptance, the SQL Server backing store the emulator requires, the bind-mounted config file, and both AMQP endpoints.
- **Startup ordering is handled.** The emulator always waits on its SQL Server resource through `WaitFor`.
- **Fails fast on a broken setup.** A `null` resolved connection string throws a `DistributedApplicationException` instead of surfacing later as a confusing connection error downstream.

## 🚀 Quick Start

This package is **not published to NuGet** — consume it as a project reference from your AppHost:

```bash
git clone https://github.com/baoduy/DKNet.git
cd YourAppHostProject
dotnet add reference ../DKNet/src/Aspire/Aspire.Hosting.ServiceBus/Aspire.Hosting.ServiceBus.csproj
```

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

Run with `dotnet run` from the AppHost project. Starting the emulator itself requires Docker.

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/Aspire.Hosting.ServiceBus/index.md) | Start with architecture and the end-to-end flow. |
| [Register the Service Bus emulator resource](https://github.com/baoduy/DKNet/blob/main/docs/Aspire.Hosting.ServiceBus/register-emulator-resource.md) | Add the emulator container, EULA acceptance, SQL Server wiring and bind-mounted config file with one call. |
| [Redirect the connection string to a real namespace](https://github.com/baoduy/DKNet/blob/main/docs/Aspire.Hosting.ServiceBus/redirect-connection-string.md) | Apply a `ConnectionStringRedirectAnnotation` so referencing projects resolve a real namespace instead of the emulator. |
