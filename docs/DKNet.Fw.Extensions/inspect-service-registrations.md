# Inspect service registrations before adding duplicates

Part of [DKNet.Fw.Extensions](index.md). Use `ServiceCollectionRegistrationExtensions` and `ServiceCollectionExtensions` in the `Microsoft.Extensions.DependencyInjection` namespace.

None — no DI registration needed; these are static extension methods you call directly on an `IServiceCollection` or `ServiceDescriptor`.

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

## ⚙️ Configuration reference

None — these registration checks read no options and expose no override point.

## 🧱 Where it fits

Every registration guard below is "register once, skip on a second call" — they let a host call an `Add*` method more than once (or combine two opt-in features) without duplicate registrations.

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| `IdempotencyMsSqlSetup` | [DKNet.AspCore.Idempotency.MsSqlStore](../DKNet.AspCore.Idempotency.MsSqlStore/register-store.md) | `IsRegistered<IdempotencyDbContext>()` skips re-registering the store's `DbContext`. |
| `IdempotencyNpgsqlSetup` | [DKNet.AspCore.Idempotency.NpgsqlStore](../DKNet.AspCore.Idempotency.NpgsqlStore/register-store.md) | `IsRegistered<IdempotencyDbContext>()` skips re-registering the store's `DbContext`. |
| `IdempotencyRedisSetup` | [DKNet.AspCore.Idempotency.RedisStore](../DKNet.AspCore.Idempotency.RedisStore/register-redis-store.md) | `IsRegistered<IDistributedCache>()` and `IsRegistered<IConnectionMultiplexer>()` each guard their own registration, so an app with its own Redis connection keeps it. |
| `IdempotencySetup` | [DKNet.AspCore.Idempotency](../DKNet.AspCore.Idempotency/choose-store.md) | `IsRegistered<IIdempotencyKeyStore>()` makes `AddIdempotentKey()`/`AddIdempotentKey<TStore>()` a no-op once a store is already registered. |
| `EventSetup` | [DKNet.EfCore.Events](../DKNet.EfCore.Events/publish-domain-events.md) | `IsRegisteredWithImplementation<IEventPublisher>(typeof(TImplementation))` lets `AddEventPublisher<TDbContext, TImplementation>()` guard against registering the same publisher implementation twice. |
| `EfCoreAuditLogSetup` | [DKNet.EfCore.AuditLogs](../DKNet.EfCore.AuditLogs/stamp-current-user.md) | `IsRegistered<ICurrentUserProvider>()` and `IsRegistered<IOptions<AuditLogOptions>>()` keep `AddCurrentUserProvider<TDbContext, TProvider>()` idempotent and avoid overwriting already-configured options. |
| `EfCoreDataAuthSetup` | [DKNet.EfCore.DataAuthorization](../DKNet.EfCore.DataAuthorization/stamp-ownership.md) | `IsRegistered<IDataOwnerProvider>()` makes `AddDataOwnerProvider<TDbContext, TProvider>()` a no-op on a second call. |
| `SetupEfCoreHook` | [DKNet.EfCore.Hooks](../DKNet.EfCore.Hooks/write-save-hooks.md) | `services.Any(s => s.IsKeyedImplementationOf<THook>(key))` on each `ServiceDescriptor` lets `AddHook<TDbContext, THook>()` skip a hook already registered for that context/hook pair, and the same check guards the shared `HookRunnerInterceptor` registration. |

All eight consumers are internal setup classes; most have no page naming them directly, so the links above go to the page documenting the `Add*` method the guard protects.

**Entry point in your app:** calling `IsRegistered`/`IsRegisteredWithImplementation`/`IsImplementationOf`/`IsKeyedImplementationOf` directly on an `IServiceCollection` or `ServiceDescriptor`, typically inside your own `Add*` extension method.

## ⚠️ Gotchas & limits

- Factory and instance registrations do not expose their concrete type through `ImplementationType`; exact implementation checks do not inspect them.
- Key matching uses `ReferenceEquals`, so equal values held in different key objects do not match.
- The keyed helper delegates to `IsImplementationOf`, which reads `ImplementationType` rather than `KeyedImplementationType`. A keyed registration can match its service type while returning false for its concrete keyed implementation.
- The checks do not provide synchronization around later mutations of the service collection.
