# Aspire.Hosting.ServiceBus

[![NuGet](https://img.shields.io/nuget/v/Aspire.Hosting.ServiceBus)](https://www.nuget.org/packages/Aspire.Hosting.ServiceBus/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Aspire.Hosting.ServiceBus)](https://www.nuget.org/packages/Aspire.Hosting.ServiceBus/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

Hosting integration that adds the Azure Service Bus **emulator** as a resource inside a
[.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) AppHost, so a message-driven workflow runs locally
under Aspire's orchestrator instead of a hand-rolled container setup.

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
- **Fail-fast validation** — throws a `DistributedApplicationException` if the resolved connection string ever comes back `null`.

## Quick Start

```csharp
using Aspire.Hosting.ServiceBus;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");
var serviceBus = builder.AddServiceBus(sql, configFilePath: "servicebus-config.json");

builder.AddProject<Projects.Api>("api")
    .WithReference(serviceBus);

builder.Build().Run();
```

## Configuration — `AddServiceBus`

```csharp
public static IResourceBuilder<ServiceBusResource> AddServiceBus(
    this IDistributedApplicationBuilder builder,
    IResourceBuilder<SqlServerServerResource> sqlServer,
    string configFilePath,
    string name = "AzureBusSimulator")
```

| Parameter | Required | Default | Effect |
|---|---|---|---|
| `sqlServer` | yes | — | The SQL Server resource the emulator uses as its backing store. Its name and password parameter become `SQL_SERVER` and `MSSQL_SA_PASSWORD`, and the emulator waits on it. |
| `configFilePath` | yes | — | Host path to the emulator's `Config.json` (namespaces, queues, topics, subscriptions), bind-mounted read-only at `/ServiceBus_Emulator/ConfigFiles/Config.json`. |
| `name` | no | `"AzureBusSimulator"` | The Aspire resource name. |

Fixed by the extension and **not** configurable through parameters:

| Concern | Value |
|---|---|
| Image | `mcr.microsoft.com/azure-messaging/servicebus-emulator:latest` |
| EULA | `ACCEPT_EULA=Y`, set automatically |
| Endpoints | `tcp` on host/container port `5672`, `tcp2` on `5671` |
| Connection string | `Endpoint=sb://{PrimaryEndpoint.Host};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;` |

There is no fluent API for declaring queues or topics in code — that topology lives entirely in `Config.json`.

`ServiceBusResource` exposes `ConnectionStringExpression`, `PrimaryEndpoint`, and
`GetConnectionStringAsync(CancellationToken)`; the first and last honour a `ConnectionStringRedirectAnnotation`
when one has been applied, so the emulator can be swapped for a real namespace without changing anything
downstream.

## Documentation

Full feature walkthrough, wiring diagram, and gotchas:
https://github.com/baoduy/DKNet/blob/main/docs/Aspire/Aspire.Hosting.ServiceBus.md
