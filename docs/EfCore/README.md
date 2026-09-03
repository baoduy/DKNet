# EF Core Packages

`DKNet.EfCore.*` is the Infrastructure Layer of the DKNet framework: EF Core building blocks for
entities, persistence, and the SaveChanges pipeline, built for Domain-Driven Design and Onion
Architecture. Each package below is independent and opt-in — pull in only what a given
`DbContext` needs.

## Foundation

- [DKNet.EfCore.Abstractions](./DKNet.EfCore.Abstractions.md) — entity base classes
  (`Entity`, `AuditedEntity`), domain-event contracts (`IEventEntity`), and the attributes
  (`[AuditLog]`, `[SensitiveData]`, `[RaisesEvent]`, `[Sequence]`, …) that every other package
  in this list reads. Start here.
- [DKNet.EfCore.Extensions](./DKNet.EfCore.Extensions.md) — the DI/wiring layer: automatic
  entity configuration discovery, global query filters, data seeding, GUIDv7 keys, sequences,
  and the `SnapshotContext` shared by the hook-based packages below.

## Querying & persistence

- [DKNet.EfCore.Specifications](./DKNet.EfCore.Specifications.md) — the specification pattern
  and `IRepositorySpec`, including the Dynamic Predicate Builder. The current, supported way to
  query and persist through a `DbContext`.
- [Migrating-Repos-To-Specifications](./Migrating-Repos-To-Specifications.md) — `DKNet.EfCore.Repos`
  and `DKNet.EfCore.Repos.Abstractions` were removed; this is the call-site mapping for consumers
  still upgrading off them onto Specifications.

## SaveChanges pipeline

- [DKNet.EfCore.Hooks](./DKNet.EfCore.Hooks.md) — the before/after-SaveChanges interceptor
  pipeline (`IBeforeSaveHookAsync`/`IAfterSaveHookAsync`) that Events, AuditLogs, and
  DataAuthorization all register against.
- [DKNet.EfCore.Events](./DKNet.EfCore.Events.md) — dispatches domain events raised by
  entities (`AddEvent`) as part of that pipeline.
- [DKNet.EfCore.AuditLogs](./DKNet.EfCore.AuditLogs.md) — captures an audit trail of entity
  changes, with `[SensitiveData]`-aware redaction.
- [DKNet.EfCore.DataAuthorization](./DKNet.EfCore.DataAuthorization.md) — row-level,
  ownership-based data authorization via global query filters.

## Data protection & generation

- [DKNet.EfCore.Encryption](./DKNet.EfCore.Encryption.md) — transparent column-level
  encryption via an EF Core value converter.
- [DKNet.EfCore.DtoGenerator](./DKNet.EfCore.DtoGenerator.md) — compile-time DTO generation
  from entities via a Roslyn source generator. See also the
  [Global Exclusions Guide](./GLOBAL_EXCLUSIONS_GUIDE.md).

## Utilities

- [DKNet.EfCore.Relational.Helpers](./DKNet.EfCore.Relational.Helpers.md) — small
  provider-aware `DbContext` helpers (table existence/creation, table name resolution).

## How the pieces fit together

Every row is the package's own `ProjectReference` set in `src/EfCore/` — nothing else in the family is pulled in
implicitly.

| Package | Depends on (inside DKNet) | Attaches to your `DbContext` via |
|---|---|---|
| `DKNet.EfCore.Abstractions` | *nothing* | base classes you derive from |
| `DKNet.EfCore.Extensions` | `DKNet.Fw.Extensions`, `Abstractions` | `UseAutoConfigModel<TContext>()` |
| `DKNet.EfCore.Hooks` | `DKNet.Fw.Extensions`, `Extensions` | `AddDbContextWithHook<TDbContext>()` |
| `DKNet.EfCore.Specifications` | `Extensions` | `AddSpecRepo<TDbContext>()` |
| `DKNet.EfCore.Events` | `Abstractions`, `Hooks` | a hook, plus `AddEventPublisher<TDbContext, TImpl>()` |
| `DKNet.EfCore.AuditLogs` | `Abstractions`, `Hooks` | a hook, plus `AddEfCoreAuditLogs<TDbContext, TPublisher>()` |
| `DKNet.EfCore.DataAuthorization` | `Extensions`, `Hooks` | a global query filter and a hook, via `AddDataOwnerProvider<TDbContext, TProvider>()` |
| `DKNet.EfCore.Encryption` | *nothing* | a `ValueConverter`, via `AddEfCoreEncryption<TKeyProvider>()` |
| `DKNet.EfCore.DtoGenerator` | *nothing* | compile time only — nothing is registered |
| `DKNet.EfCore.Relational.Helpers` | *nothing* | plain `DbContext` extension methods |

Two consequences worth remembering:

- **`Events`, `AuditLogs`, and `DataAuthorization` all need the hook pipeline.** Register the context with
  `AddDbContextWithHook<TDbContext>()`, not `AddDbContext`, or their hooks never run.
- **`Encryption`, `DtoGenerator`, and `Relational.Helpers` are independent.** They work on a `DbContext` that uses
  none of the rest of this family.

The same picture across the whole suite, plus a request and a domain event traced end to end, is in the
[Architecture Guide](../Architecture.md).

![Package dependency map of the DKNet onion: presentation, application and infrastructure packages all depend inward toward DKNet.EfCore.Abstractions, with Events, AuditLogs and DataAuthorization attaching through DKNet.EfCore.Hooks.](../diagrams/dknet-onion-packages.svg)
