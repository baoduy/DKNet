# Aspire.Hosting.ServiceBus

Hosting integration that adds the Azure Service Bus **emulator** as a resource inside a [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) AppHost, so distributed applications can run a message-driven workflow locally under Aspire's orchestrator instead of a hand-rolled container setup.

## Installation

```bash
dotnet add package Aspire.Hosting.ServiceBus
```

Reference it from your AppHost project only.

## Features

- **One-call emulator registration** — `AddServiceBus` adds the `azure-messaging/servicebus-emulator` container, accepts its EULA, and wires `SQL_SERVER`/`MSSQL_SA_PASSWORD` to your SQL Server resource automatically.
- **Config-file topology** — queues, topics, and subscriptions are declared in a bind-mounted `Config.json`, versioned alongside your AppHost.
- **Automatic startup ordering** — the emulator always waits for its SQL Server backing store via `WaitFor`.
- **Connection string wiring** — `ServiceBusResource` implements `IResourceWithConnectionString`, so downstream projects get the emulator connection string through `WithReference`, and can be redirected to a real Service Bus connection string via Aspire's `ConnectionStringRedirectAnnotation`.
- **Fail-fast validation** — throws a `DistributedApplicationException` if the resolved connection string ever comes back null.

## Quick Start

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");
var serviceBus = builder.AddServiceBus(sql, configFilePath: "servicebus-config.json");

builder.AddProject<Projects.Api>("api")
    .WithReference(serviceBus);

builder.Build().Run();
```

## Documentation

Full feature walkthrough, configuration table, and gotchas: [Aspire.Hosting.ServiceBus docs](https://github.com/baoduy/DKNet/blob/dev/docs/Aspire/Aspire.Hosting.ServiceBus.md)
