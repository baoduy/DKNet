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
- [DKNet.EfCore.Repos](./DKNet.EfCore.Repos.md) — **retired**, source-only generic repository,
  superseded by Specifications. Kept for existing consumers.
- [DKNet.EfCore.Repos.Abstractions](./DKNet.EfCore.Repos.Abstractions.md) — **retired**,
  source-only repository interfaces implemented by `DKNet.EfCore.Repos`.
- [Migrating-Repos-To-Specifications](./Migrating-Repos-To-Specifications.md) — call-site
  mapping for moving off `DKNet.EfCore.Repos`/`Repos.Abstractions` onto Specifications.

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

```
Abstractions  (entity base classes, event/audit attributes)
     ↑
Extensions    (DI wiring, global query filters, SnapshotContext)
     ↑
Hooks         (SaveChanges interceptor pipeline)
     ↑
Events · AuditLogs · DataAuthorization   (each registers a hook)

Specifications (queries + writes via IRepositorySpec)  ─┐
Repos / Repos.Abstractions (retired)                    ┴─ both consume Abstractions entities

Encryption      (EF Core value converter, independent of the hook pipeline)
DtoGenerator    (compile-time, independent of the hook pipeline)
Relational.Helpers (standalone DbContext utilities)
```
