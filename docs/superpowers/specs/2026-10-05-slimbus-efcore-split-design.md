# SlimBus: split into an EF-free core and an EF Core add-on — Design

**Date:** 2026-10-05
**Status:** Approved design. The docs in `docs/` have been updated to this design; code not yet implemented.
**Reference:** [SlimMessageBus — Use Case: Replace MediatR](https://github.com/zarusz/SlimMessageBus/blob/master/docs/UseCases/ReplaceMediatR.md)

## Goal

Offer two separate SlimBus configurations:

1. **Without EF Core** — a MediatR-style, in-process command/query bus on SlimMessageBus's memory provider. A project
   that references it carries no `Microsoft.EntityFrameworkCore*` assembly, transitively.
2. **With EF Core** — the same bus plus today's EF behaviour: auto-`SaveChanges` after a successful write request and
   domain events forwarded onto the bus.

**Success check:** `dotnet list SlimBus/DKNet.SlimBus.Extensions package --include-transitive` lists no
`Microsoft.EntityFrameworkCore*` package.

## Why a package split

`DKNet.SlimBus.Extensions` pulls EF Core in through two routes, so adding a second setup method to it cannot satisfy
"without EF Core":

- the project reference to `DKNet.EfCore.Events`
- the `X.PagedList.EF` package, used only for the `IPagedList<T>` interface in `Fluents.cs`; that interface lives in
  the base `X.PagedList` package, which has no EF dependency.

## Packages

| | `DKNet.SlimBus.Extensions` (core, EF-free) | `DKNet.SlimBus.Extensions.EfCore` (new) |
|---|---|---|
| Contents | `Fluents`, `LazyMapper/*`, `NotFoundError`, new `SlimBusSetup` | `Interceptors/*`, `Handlers/SlimBusEventPublisher`, `SlimBusEfCoreSetup` (moved) |
| Dependencies | FluentResults, Mapster, Microsoft.Extensions.Hosting.Abstractions, SlimMessageBus, SlimMessageBus.Host, SlimMessageBus.Host.Interceptor, **SlimMessageBus.Host.Memory** (new), **X.PagedList** (replaces X.PagedList.EF) | `DKNet.SlimBus.Extensions`, `DKNet.EfCore.Events` |
| Namespaces | unchanged | moved types keep `DKNet.SlimBus.Extensions.Interceptors` / `.Handlers`; the setup class keeps the ambient `Microsoft.Extensions.DependencyInjection` namespace |

Because namespaces are unchanged, `DKNet.SlimBus.Generators` output compiles as-is. Generated `[CrudAction]` slices
rely on auto-save, so their hosts reference the `.EfCore` package.

`DKNet.AspCore.Extensions` keeps referencing the core only; it still gets `X.PagedList.EF` (for `ToPagedListAsync`)
through `DKNet.EfCore.Specifications`.

## Setup API

```csharp
// DKNet.SlimBus.Extensions — MediatR replacement, no EF Core.
public static IServiceCollection AddSlimBusForMediator(
    this IServiceCollection services, params Assembly[] handlerAssemblies);
// = services.AddSlimMessageBus(mbb => {
//       mbb.WithProviderMemory();
//       foreach (var asm in handlerAssemblies) {
//           mbb.AddServicesFromAssembly(asm);   // register handlers in DI
//           mbb.AutoDeclareFrom(asm);           // declare them on the memory bus
//       } });
// (Use an Assembly[]/IEnumerable<Assembly> overload instead if SlimMessageBus 3.5 has one — verify at implementation.)

// DKNet.SlimBus.Extensions.EfCore — the same bus plus auto-save and domain events.
public static IServiceCollection AddSlimBusForEfCore<TDbContext>(
    this IServiceCollection services, params Assembly[] handlerAssemblies) where TDbContext : DbContext;
// = services.AddSlimBusForMediator(asm…)
//       .AddSlimBusEfCoreInterceptor<TDbContext>()
//       .AddSlimBusEventPublisher<TDbContext>();
```

- `handlerAssemblies` must not be empty: throw `ArgumentException` — a bus with no handlers is a configuration bug.
- No serializer is registered: the memory provider passes messages by reference unless serialization is enabled,
  matching the SlimMessageBus MediatR use case.
- No `Action<MessageBusBuilder>` hook and no child buses. Hosts that need a real transport (Service Bus, Kafka, hybrid)
  keep calling `AddSlimMessageBus` themselves; `AddSlimBusEfCoreInterceptor<T>()` and `AddSlimBusEventPublisher<T>()`
  stay public for exactly that.
- `AddSlimBusForEfCore<T>` is for one `DbContext`. A second context calls `AddSlimBusEfCoreInterceptor<TOther>()`
  (and `AddSlimBusEventPublisher<TOther>()`) on top — it must not call `AddSlimBusForMediator` twice.

## Breaking change

Callers of `AddSlimBusEfCoreInterceptor<T>()` or `AddSlimBusEventPublisher<T>()` must add a reference to
`DKNet.SlimBus.Extensions.EfCore`. No `using` changes. Recorded in `docs/CHANGELOG.md`.

## Code changes (implementation phase)

- `src/Directory.Packages.props`: add `PackageVersion` for `X.PagedList` (same version as `X.PagedList.EF`, 10.6.0).
- `DKNet.SlimBus.Extensions.csproj`: drop the `DKNet.EfCore.Events` reference; `X.PagedList.EF` → `X.PagedList`; add
  `SlimMessageBus.Host.Memory`. Add `SlimBusSetup.cs` at the project root (DI entry surface).
- New `src/SlimBus/DKNet.SlimBus.Extensions.EfCore/` project; move the three EF files plus `SlimBusEfCoreSetup.cs`;
  add `AddSlimBusForEfCore<T>`; `InternalsVisibleTo` `SlimBus.Extensions.Tests`; add `README.md`; add to `DKNet.FW.sln`.
- Re-point project references that use the EF setup: `SlimBus.Extensions.Tests`, `SlimBus.Generators.Tests.Api`,
  `EfCore.Extensions.Tests` (if it calls the SlimBus EF helpers).
- `PublicSurfaceGuardTests` anchors on `typeof(SlimBusEventPublisher).Assembly`; re-anchor it on `Fluents` and run
  the guard over both assemblies.

## Testing

- Keep one test project, `SlimBus.Extensions.Tests`, referencing both packages; the existing auto-save, registry and
  publisher tests move with no behavioural change.
- New: `AddSlimBusForMediator_SendsRequest_WithoutDbContext` — build a provider with only `AddSlimBusForMediator`,
  send a `Fluents.Requests` command and a `Fluents.Queries` query, assert results.
- New: `AddSlimBusForMediator_NoAssemblies_Throws`.
- New: `AddSlimBusForEfCore_WriteRequest_AutoSaves` — the one-call EF setup saves after a successful write.
- Package check: `dotnet list ... --include-transitive` on the core shows no EF Core (manual / CI step).

## Docs and skills

- Done in this change (docs only): `docs/Messaging/DKNet.SlimBus.Extensions.md`, new
  `docs/Messaging/DKNet.SlimBus.Extensions.EfCore.md`, `docs/Messaging/README.md`,
  `docs/Messaging/DKNet.SlimBus.Generators.md`, `docs/Architecture.md`, `docs/Configuration.md`, `docs/FAQ.md`,
  `docs/Examples/README.md`, `docs/API-Reference.md`, `docs/README.md`, `docs/Getting-Started.md`,
  `docs/Core/DKNet.Fw.Extensions.md`, `docs/EfCore/DKNet.EfCore.Events.md`, `docs/CHANGELOG.md`.
- With the code: the `dknet-slimbus-cqrs` skill (`SKILL.md` + `references/`). Its snippet-lab compiles against
  nuget.org, so snippets using `.EfCore` fail until the package is published.
- Done in this change: `docs/diagrams/dknet-onion-packages`, `dknet-layers` and `dknet-request-lifecycle` (`.json`
  + `.svg`, re-rendered with archify 3.0.1). `slimbus-request-pipeline` names no package and is unchanged.

## Out of scope

- FluentValidation integration, generic logging interceptors, hybrid provider — SlimMessageBus features consumers wire
  themselves.
- Moving `Fluents` out of the `DKNet.SlimBus.Extensions` namespace.
