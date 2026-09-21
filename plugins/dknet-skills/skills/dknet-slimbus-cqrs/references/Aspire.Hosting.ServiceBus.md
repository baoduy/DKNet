# Aspire.Hosting.ServiceBus

| Field | Value |
|---|---|
| Area | Aspire |
| NuGet | source-only, not packable — the project sets `IsPackable=false`; reference the project directly (`ProjectReference`), it is not on NuGet |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/Aspire/Aspire.Hosting.ServiceBus.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/Aspire/Aspire.Hosting.ServiceBus |
| Depends on (DKNet) | none |
| Depends on (3rd party) | `Aspire.Hosting`, `Aspire.Hosting.SqlServer`, `Microsoft.Extensions.Diagnostics.HealthChecks`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Logging.Console`, `Microsoft.Extensions.Logging.Debug` |
| Target framework | `net10.0` |

## Purpose

`AddServiceBus` is a single Aspire AppHost extension method that registers the Azure Service Bus
**emulator** (`mcr.microsoft.com/azure-messaging/servicebus-emulator:latest`) as a `ContainerResource`,
wiring its mandatory SQL Server backing store, EULA acceptance, config-file bind mount, two AMQP
endpoints, and startup ordering in one call. The returned `ServiceBusResource` implements
`IResourceWithConnectionString`, so a downstream project gets the emulator's connection string via the
normal `WithReference` flow, and that connection string can be redirected to a real Azure Service Bus
namespace via Aspire's own `ConnectionStringRedirectAnnotation` without touching handler code.

**Not**: a way to provision or reference a real Azure Service Bus namespace (it only ever runs the
emulator container), a fluent API for declaring queues/topics/subscriptions (that topology lives
entirely in the bind-mounted `Config.json`), a message-handling library (message handling is
`DKNet.SlimBus.Extensions`, consumed by the *referencing* API/worker project, not this AppHost-only
package), or a health-check registration (none is wired).

## Entry points

| Call | Exact signature | Called on | Notes |
|---|---|---|---|
| `AddServiceBus` | `static IResourceBuilder<ServiceBusResource> AddServiceBus(this IDistributedApplicationBuilder builder, IResourceBuilder<SqlServerServerResource> sqlServer, string configFilePath, string name = "AzureBusSimulator")` | `IDistributedApplicationBuilder` (AppHost project only) | Must be called with an already-registered SQL Server resource (e.g. from `builder.AddSqlServer(...)`); the returned resource always `WaitFor(sqlServer)`, so build order is SQL Server ready → emulator starts. Subscribes to `builder.Eventing`'s `ConnectionStringAvailableEvent` for the new resource at call time — that subscription throws `DistributedApplicationException` at Aspire's event-publish time (not at `AddServiceBus` call time) if the resolved connection string is `null`. |

There are no `ModelBuilder`/`DbContextOptionsBuilder`/attribute/analyzer entry points in this package —
it is a pure Aspire hosting extension.

## Public surface

### `Aspire.Hosting.ServiceBus`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `ServiceBusExtensions` | static class | Entry-point extension methods for AppHost builders | `static IResourceBuilder<ServiceBusResource> AddServiceBus(this IDistributedApplicationBuilder builder, IResourceBuilder<SqlServerServerResource> sqlServer, string configFilePath, string name = "AzureBusSimulator")` |
| `ServiceBusResource` | sealed class, `ContainerResource(name)`, implements `IResourceWithConnectionString` | The emulator resource type; carries the connection-string logic and endpoint reference | Ctor: `ServiceBusResource(string name)`. Properties: `ReferenceExpression ConnectionStringExpression { get; }`, `EndpointReference PrimaryEndpoint { get; }`. Method: `ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default)`. **Internal, not consumer-visible**: `PrimaryEndpointName` (`"tcp"`), `SecondaryEndpointName` (`"tcp2"`). |

No enums, records, interfaces, or attributes are declared in this package. `SqlServerServerResource`,
`IDistributedApplicationBuilder`, `IResourceBuilder<T>`, `ContainerResource`,
`IResourceWithConnectionString`, `ConnectionStringRedirectAnnotation`, `EndpointReference`,
`ReferenceExpression`, and `ConnectionStringAvailableEvent` are all Aspire's own types
(`Aspire.Hosting`/`Aspire.Hosting.ApplicationModel`/`Aspire.Hosting.SqlServer`), not defined here.

## Options & defaults

No options object exists. `AddServiceBus`'s own parameters are the only configuration surface:

| Option | Type | Default | Effect |
|---|---|---|---|
| `sqlServer` | `IResourceBuilder<SqlServerServerResource>` | — (required) | Backing store; supplies `SQL_SERVER` and `MSSQL_SA_PASSWORD` env vars; the emulator `WaitFor`s it |
| `configFilePath` | `string` | — (required) | Host path bind-mounted read-only to `/ServiceBus_Emulator/ConfigFiles/Config.json` inside the container |
| `name` | `string` | `"AzureBusSimulator"` | The Aspire resource name used for the `ServiceBusResource` |

Fixed by the implementation, not exposed as parameters at all: image `azure-messaging/servicebus-emulator`,
registry `mcr.microsoft.com`, tag `latest`, `ACCEPT_EULA=Y`, endpoint `tcp` on port `5672`, endpoint
`tcp2` on port `5671` (both endpoint names are `internal`).

## Usage patterns

### Register the emulator against a SQL Server resource and reference it from an API project

**When**: standard local-dev AppHost wiring for a message-driven workflow.

```csharp
// AppHost/Program.cs
using Aspire.Hosting.ServiceBus;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");

var serviceBus = builder.AddServiceBus(sql, configFilePath: "servicebus-config.json");

// The referencing API project reads the connection string the normal Aspire way and hands it to
// SlimMessageBus.Host.AzureServiceBus (the API project references that transport directly —
// DKNet.SlimBus.Extensions brings no transport of its own):
// builder.AddProject<Projects.Api>("api").WithReference(serviceBus);

builder.Build().Run();
```

**Notes**: `configFilePath` is resolved relative to the AppHost's working directory at run time —
commit `servicebus-config.json` alongside the AppHost project. `WithReference(serviceBus)` injects the
connection string into the referencing project under Aspire's standard resource-name-based
configuration key.

### Give the emulator resource a custom Aspire resource name

**When**: running more than one Service Bus emulator in the same AppHost (e.g. per-tenant local
setups), or just to make the dashboard name match your domain.

```csharp
using Aspire.Hosting.ServiceBus;

public static class CustomNameExample
{
    public static void Configure(string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);
        var sql = builder.AddSqlServer("sql");

        var serviceBus = builder.AddServiceBus(sql, configFilePath: "servicebus-config.json", name: "orders-bus");

        builder.Build().Run();
    }
}
```

**Notes**: only the Aspire resource `name` changes — the container's AMQP ports (`5672`/`5671`) are
fixed by the extension, so two emulator instances in the same AppHost still collide on host ports
regardless of `name`.

### Redirect the emulator's connection string to a real Azure Service Bus namespace

**When**: promoting a downstream project's configuration path from local emulator to a real namespace
without touching handler code, using Aspire's own redirection annotation.

```csharp
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ServiceBus;

public static class RedirectExample
{
    public static void Configure(string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);
        var sql = builder.AddSqlServer("sql");
        var serviceBus = builder.AddServiceBus(sql, configFilePath: "servicebus-config.json");

        // Somewhere else in the AppHost, when promoting past local dev:
        // serviceBus.Resource.Annotations.Add(
        //     new ConnectionStringRedirectAnnotation(realServiceBusResource.Resource));

        builder.Build().Run();
    }
}
```

**Notes**: `ConnectionStringExpression` and `GetConnectionStringAsync` both check for a
`ConnectionStringRedirectAnnotation` on the resource before building the emulator's own `sb://...`
expression; the annotation itself is Aspire's own type (`Aspire.Hosting.ApplicationModel`) — this
package supplies no helper to attach it, only the resource's willingness to honour it once attached.

### Reference the primary endpoint directly (e.g. for a custom health check)

**When**: you need the emulator's host/port outside the connection string, since this package registers
no health check itself.

```csharp
using Aspire.Hosting.ServiceBus;

public static class PrimaryEndpointExample
{
    public static void Configure(string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);
        var sql = builder.AddSqlServer("sql");
        var serviceBus = builder.AddServiceBus(sql, configFilePath: "servicebus-config.json");

        var host = serviceBus.Resource.PrimaryEndpoint.Host; // available once the resource has an endpoint

        builder.Build().Run();
    }
}
```

**Notes**: `PrimaryEndpoint` is a lazily-created `EndpointReference`; `PrimaryEndpointName` itself
(`"tcp"`) is `internal`, so a consumer can read `PrimaryEndpoint` but cannot pass the endpoint name
string as a compile-time constant from outside the assembly.

## Runtime behaviour

1. `AddServiceBus` constructs a `ServiceBusResource(name)` and subscribes a handler to
   `builder.Eventing`'s `ConnectionStringAvailableEvent`, scoped to that resource instance, *before*
   adding the resource to the builder.
2. `builder.AddResource(bus)` registers the resource, then the fluent chain applies, in order:
   image/registry/tag, `ACCEPT_EULA=Y`, `SQL_SERVER`/`MSSQL_SA_PASSWORD` env vars from `sqlServer`, the
   `Config.json` bind mount, the two `tcp`/`tcp2` endpoints, then `WaitFor(sqlServer)`.
3. At Aspire orchestration time, the emulator container does not start until the SQL Server resource it
   waits on reports ready.
4. When Aspire resolves the resource's connection string and publishes `ConnectionStringAvailableEvent`
   for `bus`, the subscribed handler from step 1 calls `bus.GetConnectionStringAsync(ct)`; if that comes
   back `null`, it throws `DistributedApplicationException` immediately, failing AppHost startup fast
   instead of letting a downstream project fail later with a confusing connection error.
5. `GetConnectionStringAsync`/`ConnectionStringExpression` check for a `ConnectionStringRedirectAnnotation`
   on the resource first; if present, they delegate to the redirect target's own connection string
   instead of building the emulator's `sb://...` expression.
6. A downstream project that calls `.WithReference(serviceBus)` receives that resolved connection string
   through Aspire's normal configuration injection — this package does nothing further once the string
   is resolved.

## Diagnostics & exceptions

| Type | Severity | When | Fix |
|---|---|---|---|
| `DistributedApplicationException` | Error (fails AppHost startup) | Thrown inside the `ConnectionStringAvailableEvent` handler `AddServiceBus` subscribes, when `bus.GetConnectionStringAsync(ct)` resolves to `null` for the `ServiceBusResource` | Check the resource isn't misconfigured (e.g. a `ConnectionStringRedirectAnnotation` pointing at a resource with no connection string) — this is a fail-fast guard, not a recoverable condition. |

No analyzer `DiagnosticDescriptor`s exist in this package — it defines no Roslyn analyzer.

## Gotchas

- **`PrimaryEndpointName` (`"tcp"`) and `SecondaryEndpointName` (`"tcp2"`) are `internal const string`,
  not `public`.** A consumer cannot reference the endpoint name constants directly; they can only go
  through the already-exposed `PrimaryEndpoint` property. → There is no exposed way to construct a
  reference to the secondary (`tcp2`) endpoint from outside the assembly.
- **`AddServiceBus` always requires a `SqlServerServerResource` and always calls `WaitFor(sqlServer)`**
  — there is no overload without it. → You cannot use this package to model an emulator-less or
  SQL-Server-less topology; the emulator genuinely requires SQL Server as its backing store.
- **The `DistributedApplicationException` for a null connection string is thrown from an event handler,
  not from `AddServiceBus` itself.** → A `try`/`catch` wrapped around the `AddServiceBus(...)` call will
  never see it — the throw happens later, when Aspire publishes `ConnectionStringAvailableEvent`. Let
  AppHost startup fail fast as designed instead.
- **Image tag is hard-coded to `latest` and both ports (`5672`, `5671`) are hard-coded** — no parameter
  pins a version or remaps a port. → Two `AddServiceBus` calls in one AppHost collide on host ports;
  there is also no reproducible-build guarantee since `latest` can change under you. Register only one
  Service Bus emulator resource per AppHost.
- **There is no fluent API (no `WithQueue`/`WithTopic`) for declaring topology in code.** → Hallucinated
  code referencing `serviceBus.WithQueue(...)` will not compile; all topology (namespaces, queues,
  topics, subscriptions) goes in the `Config.json` passed as `configFilePath`.
- **`IsPackable=false` on the project** means `dotnet pack`/CI's NuGet packing step does not produce a
  `.nupkg` for this project, even though the docs and README instruct `dotnet add package
  Aspire.Hosting.ServiceBus`. → Verify the package actually exists on the feed you're pulling from
  before relying on that instruction; otherwise reference the project directly (`ProjectReference`), as
  the AppHost does not require NuGet packaging for a single-solution AppHost.
- **No health check is registered by `AddServiceBus`.** → A project `WaitFor`/readiness-gating against
  `serviceBus` before it's actually ready to accept connections will race; add your own Aspire health
  check for the resource if you need readiness gating beyond container-start ordering.

## Anti-patterns & hallucination traps

- `serviceBus.WithQueue(...)` / `.WithTopic(...)` / `.WithSubscription(...)` — **do not exist.** All
  topology is declared in the `Config.json` file passed as `configFilePath`; there is no fluent builder
  surface for it on `ServiceBusResource` or in `ServiceBusExtensions`.
- `ServiceBusResource.PrimaryEndpointName`/`SecondaryEndpointName` used from a consuming project — these
  constants are `internal`, not `public`; code outside this assembly cannot reference them by name.
- `AddServiceBus(builder, configFilePath)` (no SQL Server argument) — there is no overload without
  `sqlServer`; the parameter is required, not optional.
- `AddServiceBus(...)` called from a downstream API/worker project — this is an **AppHost-only**
  extension (`IDistributedApplicationBuilder`); it has nothing to call it against outside an AppHost.
- Registering a real Azure Service Bus namespace directly through this package — it only ever
  provisions the `azure-messaging/servicebus-emulator` container image; production wiring is either
  outside this package entirely or via `ConnectionStringRedirectAnnotation` pointed at a different
  resource (Aspire's own mechanism, not one this package adds).
- Handling messages in this package — `DKNet.SlimBus.Generators`-generated handlers and
  `Fluents.Requests.IHandler<,>` live in `DKNet.SlimBus.Extensions`, consumed by the API/worker project,
  not the AppHost; this package never appears in that project.
- Catching `DistributedApplicationException` around the `AddServiceBus(...)` call, expecting to handle
  the null-connection-string case there — the throw happens inside a later event callback
  (`ConnectionStringAvailableEvent`), not synchronously inside `AddServiceBus`.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.SlimBus.Extensions` | Reach for it in the **API/worker** project (not the AppHost) for the CQRS handler-dispatch/auto-save wiring on top of SlimMessageBus — it does **not** bundle or wrap a transport; the API/worker project separately adds SlimMessageBus's own `SlimMessageBus.Host.AzureServiceBus` provider to resolve the connection string this resource exposes via `WithReference`. |
| `DKNet.SlimBus.Generators` | Reach for it (via `[CrudAction]`) to generate the request/handler/endpoint vertical slice that ultimately publishes through the transport this package provisions — never hand-write what it emits. |
| `Aspire.Hosting.SqlServer` (3rd-party) | Required alongside this package — `AddServiceBus` always needs an `IResourceBuilder<SqlServerServerResource>` from `builder.AddSqlServer(...)`; there is no path around it. |

## Testing notes

- This package's own tests use plain xUnit + Shouldly against Aspire's own in-memory builder — **no
  TestContainers, no Docker** are needed (the emulator container itself is never actually launched).
- The canonical pattern for asserting Aspire resource wiring without starting containers: build a real
  `DistributedApplication.CreateBuilder()`, call `builder.AddSqlServer(...)` and
  `builder.AddServiceBus(sqlServer, configFilePath)` against a temp config file, then assert on
  `serviceBus.Resource.Annotations.OfType<WaitAnnotation>()` that a `WaitAnnotation` referencing that
  exact SQL Server resource instance exists — inspect `.Resource.Annotations` rather than running the
  AppHost.
- A consumer testing code that *uses* this package's `ServiceBusResource` (rather than testing this
  package itself) should follow the same pattern: build a `DistributedApplication` test builder, call
  `AddServiceBus`, and assert on the resource's annotations/endpoints rather than starting the actual
  container — starting the real emulator would require Docker and a running SQL Server resource, which
  is unnecessary for AppHost-wiring assertions.
