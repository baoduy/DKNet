# Inspect service registrations before adding duplicates

Part of [DKNet.Fw.Extensions](index.md). Use `ServiceCollectionRegistrationExtensions` and `ServiceCollectionExtensions` in the `Microsoft.Extensions.DependencyInjection` namespace.

```csharp
using System;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
if (!services.IsRegistered<IWorker>())
    services.AddSingleton<IWorker, Worker>();
Console.WriteLine(services.IsRegisteredWithImplementation<IWorker>(typeof(Worker))); // True
Console.WriteLine(services[0].IsImplementationOf<Worker>()); // True

public interface IWorker { }
public sealed class Worker : IWorker { }
```

![IsRegistered scans ServiceType for a contract match, while IsRegisteredWithImplementation also requires an exact ImplementationType match.](diagrams/service-registration.svg)

`IsRegistered<TService>()` checks for any matching service type. `IsRegisteredWithImplementation<TService>(Type)` checks the exact service and implementation pair. These helpers inspect registrations and do not register or resolve services.

For a `ServiceDescriptor`, `IsImplementationOf(Type)` / `IsImplementationOf<T>()` compares either `ServiceType` or `ImplementationType`. `IsKeyedImplementationOf(key, Type)` / `IsKeyedImplementationOf<T>(key)` first checks `IsKeyedService` and reference identity of `ServiceKey`, then delegates to the same implementation check.

## ⚠️ Gotchas & limits

- Factory and instance registrations do not expose their concrete type through `ImplementationType`; exact implementation checks do not inspect them.
- Key matching uses `ReferenceEquals`, so equal values held in different key objects do not match.
- The keyed helper delegates to `IsImplementationOf`, which reads `ImplementationType` rather than `KeyedImplementationType`. A keyed registration can match its service type while returning false for its concrete keyed implementation.
- The checks do not provide synchronization around later mutations of the service collection.
