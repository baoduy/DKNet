# Aspire

Hosting integrations that let a [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) AppHost declare DKNet's
messaging infrastructure as first-class resources.

## Packages

| Package | Description |
|---|---|
| [`Aspire.Hosting.ServiceBus`](./Aspire.Hosting.ServiceBus.md) | Registers the Azure Service Bus emulator container as an Aspire resource, wired to a SQL Server resource and an emulator config file. |

## Install

```bash
dotnet add package Aspire.Hosting.ServiceBus
```

See the package page for the real builder surface (`AddServiceBus`), its emulator-only scope today, and how it
pairs with `DKNet.SlimBus.Extensions` in worker/API projects.
