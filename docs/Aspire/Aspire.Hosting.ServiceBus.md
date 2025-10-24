# Aspire.Hosting.ServiceBus

Utilities that light up Azure Service Bus inside a [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) AppHost.
The package exposes strongly-typed resources, configuration conventions, and builder extensions to ensure your
messaging topology is defined once and consumed consistently by DKNet services.

## ✨ Key Capabilities

- **Resource abstraction** – `ServiceBusResource` models namespaces and queues/topics with metadata that maps directly to Aspire resources.
- **Builder extensions** – `AddServiceBus` helpers register namespaces, queues, and secrets in a single fluent call.
- **Connection string wiring** – Automatically injects connection secrets into projects via Aspire's parameter bindings.
- **Local/remote parity** – Works with Aspire's service discovery so local orchestrations mimic production deployments.

## 🚀 Quick Start

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var serviceBus = builder.AddServiceBus("messaging", configure =>
{
    configure.WithNamespace("dknet-dev")
             .WithQueue("commands")
             .WithTopic("events");
});

var api = builder.AddProject<Projects.Api>("api");
api.WithReference(serviceBus);

builder.Build().Run();
```

The extension registers Azure Service Bus namespaces and ensures downstream projects automatically receive
connection strings named according to the DKNet conventions.

## ⚙️ Configuration Options

`ServiceBusResource` exposes fluent methods to tailor the messaging topology:

- `WithNamespace(name)` – Sets the Azure Service Bus namespace (supports parameterisation via environment variables).
- `WithQueue(name, configure?)` – Adds queues with optional dead-letter, TTL, and partition settings.
- `WithTopic(name, configure?)` – Declares topics with subscription metadata.
- `WithSecretsFrom(connectionName)` – Rebinds connection strings when using existing Azure resources.

All resources surface Aspire's `ResourceBuilder` API so you can chain additional metadata (tags, env vars, replicas).

## 🧱 Integration Guidance

- Pair Service Bus resources with the `DKNet.SlimBus.Extensions` messaging package in worker/API projects.
- Use Aspire's parameter injection (`[FromConnectionString]`) to bind queue/topic connection strings into message handlers.
- Combine with `DKNet.AspCore.Tasks` background jobs to seed queues or purge poison messages during startup.

## ✅ Best Practices

- Keep resource declarations close to the workloads that consume them to improve readability.
- Use separate namespaces per environment and surface them via `builder.AddParameter` for secure secret management.
- Include Bicep or Terraform modules for production infrastructure and mirror the configuration names in Aspire.
- Leverage Aspire's health checks to ensure Service Bus connectivity before traffic reaches your APIs.

## 🔗 Related Resources

- [DKNet Messaging Documentation](../Messaging/README.md)
- [Azure Service Bus documentation](https://learn.microsoft.com/azure/service-bus-messaging/)
- [Aspire resource builder reference](https://learn.microsoft.com/dotnet/aspire/fundamentals/resources/)
