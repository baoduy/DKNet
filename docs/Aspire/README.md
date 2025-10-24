# DKNet Aspire Integrations

DKNet provides opinionated helpers for [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) so you can model
service dependencies declaratively inside the AppHost. The packages extend Aspire's distributed application builder
with additional resources and conventions that align with DKNet's messaging stack.

## 📦 Packages

| Package | Description |
|---------|-------------|
| [`Aspire.Hosting.ServiceBus`](./Aspire.Hosting.ServiceBus.md) | Azure Service Bus resource helpers and orchestration utilities |

## 🧱 Architecture Role

Aspire integrations sit at the **infrastructure orchestration** tier, complementing the Onion Architecture by providing
clean boundaries for how services are provisioned and discovered. They help you:

- Declare messaging infrastructure once and reuse it across multiple projects
- Inject connection strings and credentials into worker and API projects automatically
- Align SlimMessageBus or other messaging components with Azure Service Bus namespaces
- Support local developer experiences via Aspire's orchestrator while preparing for production deployments

## 🚀 Getting Started

1. Install `.NET Aspire` tooling and create an AppHost project.
2. Reference the DKNet Aspire packages from the AppHost.
3. Use the provided extension methods to register Service Bus resources alongside your workloads.
4. Bind connection details inside downstream projects via Aspire's configuration plumbing.

Refer to the individual package documentation for concrete code samples and configuration options.
