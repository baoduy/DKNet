# Diagnostics quick index

Every build-time diagnostic ID the two generators in this skill can report, sorted by prefix then number, for fast triage from a build log. Each row links to the fuller explanation (with source-verified nuance and a worked fix) in the owning package's reference file. This file is an index, not a replacement for those — read the linked row before changing code.

## `DKCRUDGEN0xx` — `DKNet.SlimBus.Generators` (`CrudGenerator`)

| ID | Severity | One line | Full row |
|---|---|---|---|
| `DKCRUDGEN001` | Error | Entity has `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` but no `[GenerateDto(typeof(Entity))]` in the project. | [SlimBus.Generators](DKNet.SlimBus.Generators.md#diagnostics--exceptions) |
| `DKCRUDGEN002` | Error | More than one `[GenerateDto(typeof(Entity))]` DTO for the same entity. | [SlimBus.Generators](DKNet.SlimBus.Generators.md#diagnostics--exceptions) |
| `DKCRUDGEN003` | Error | More than one `[CrudCreate]` member on one entity. | [SlimBus.Generators](DKNet.SlimBus.Generators.md#diagnostics--exceptions) |
| `DKCRUDGEN004` | Error | A CRUD-attributed constructor/method is not `public`. | [SlimBus.Generators](DKNet.SlimBus.Generators.md#diagnostics--exceptions) |
| `DKCRUDGEN005` | Info | A hand-written `IHandler<TRequest, ...>` was found; the generated handler was skipped — override confirmed. | [SlimBus.Generators](DKNet.SlimBus.Generators.md#diagnostics--exceptions) |
| `DKCRUDGEN006` | Error | The entity does not implement `IEntity<TKey>` (directly, or via `Entity`/`AuditedEntity`). | [SlimBus.Generators](DKNet.SlimBus.Generators.md#diagnostics--exceptions) |
| `DKCRUDGEN007` | Error | A member carries both `[CrudUpdate]` and `[CrudAction]`. | [SlimBus.Generators](DKNet.SlimBus.Generators.md#diagnostics--exceptions) |
| `DKCRUDGEN008` | Error | Two members resolve to the same route segment. | [SlimBus.Generators](DKNet.SlimBus.Generators.md#diagnostics--exceptions) |
| `DKCRUDGEN009` | Error | Two routes resolve to the same route name (including the reserved `GetById`/`GetList`/`Create`/`Delete`). | [SlimBus.Generators](DKNet.SlimBus.Generators.md#diagnostics--exceptions) |
| `DKCRUDGEN010` | Info | A member already claims the name `Delete{Entity}Request` — DELETE falls back to the request-less overload. | [SlimBus.Generators](DKNet.SlimBus.Generators.md#diagnostics--exceptions) |
| *(`ArgumentException`, runtime)* | n/a | `CrudMapOptions.Exclude`/`Configure` (string overload) named a route the entity has no route for. | [SlimBus.Generators](DKNet.SlimBus.Generators.md#diagnostics--exceptions) |

## `DKDTOGEN0xx` — `DKNet.EfCore.DtoGenerator` (`DtoGenerator`)

| ID | Severity | One line | Full row |
|---|---|---|---|
| `DKDTOGEN001` | Warning | Generation for one `[GenerateDto]` target threw (caught per-target; other DTOs still generate). | [DtoGenerator](DKNet.EfCore.DtoGenerator.md#diagnostics--exceptions) |
| `DKDTOGEN002` | Warning | Resolved entity has zero eligible properties — check the `typeof(...)` reference. | [DtoGenerator](DKNet.EfCore.DtoGenerator.md#diagnostics--exceptions) |
| `DKDTOGEN003` | Info | N properties were filtered in/out — echoes the effective Include/Exclude list. | [DtoGenerator](DKNet.EfCore.DtoGenerator.md#diagnostics--exceptions) |
| `DKDTOGEN004` | Warning | Both `Include` and `Exclude` set on one `[GenerateDto]` — **no `.g.cs` at all** is emitted for it. | [DtoGenerator](DKNet.EfCore.DtoGenerator.md#diagnostics--exceptions) |
| `DKDTOGEN005` | Info | `Include` used while `DtoGeneratorExclusions` is non-empty — the global list was ignored for that DTO. | [DtoGenerator](DKNet.EfCore.DtoGenerator.md#diagnostics--exceptions) |

## `DKRAISEVT0xx` — `DKNet.EfCore.DtoGenerator` (`RaisesEventValidator`, for `[RaisesEvent]` from `DKNet.EfCore.Abstractions`)

| ID | Severity | One line | Full row |
|---|---|---|---|
| `DKRAISEVT001` | Error | A narrowing property is a nested path or not a direct property of the entity. | [DtoGenerator](DKNet.EfCore.DtoGenerator.md#diagnostics--exceptions) |
| `DKRAISEVT002` | Error | Named payload type has no `[GenerateDto]`, or was generated from a different entity. | [DtoGenerator](DKNet.EfCore.DtoGenerator.md#diagnostics--exceptions) |
| `DKRAISEVT003` | Warning | Narrowing properties set on a rule without `EventOperations.Updated` — no runtime effect. | [DtoGenerator](DKNet.EfCore.DtoGenerator.md#diagnostics--exceptions) |
| `DKRAISEVT004` | Error | Composed convention-form name collides with an existing, incompatible type. | [DtoGenerator](DKNet.EfCore.DtoGenerator.md#diagnostics--exceptions) |
| `DKRAISEVT005` | Error | Label isn't a compile-time constant string, or the composed name isn't a valid C# identifier. | [DtoGenerator](DKNet.EfCore.DtoGenerator.md#diagnostics--exceptions) |
| `DKRAISEVT006` | Error | Two different entities in the same namespace compose the same event name. | [DtoGenerator](DKNet.EfCore.DtoGenerator.md#diagnostics--exceptions) |
| `DKRAISEVT007` | Error | A declaration names no `EventOperations` — can never raise anything. | [DtoGenerator](DKNet.EfCore.DtoGenerator.md#diagnostics--exceptions) |
| `DKRAISEVT008` | Error | Two declarations on the SAME entity compose the same event name. | [DtoGenerator](DKNet.EfCore.DtoGenerator.md#diagnostics--exceptions) |
| `DKRAISEVT009` | Error | Convention-form declaration sets both `Exclude` and `Include`. | [DtoGenerator](DKNet.EfCore.DtoGenerator.md#diagnostics--exceptions) |
| `DKRAISEVT010` | Error | `Exclude`/`Include` names a property that isn't a direct property of the entity. | [DtoGenerator](DKNet.EfCore.DtoGenerator.md#diagnostics--exceptions) |
| `DKRAISEVT011` | Error | `Exclude`/`Include` supplied on the type-naming form (`[RaisesEvent(typeof(Payload), ...)]`). | [DtoGenerator](DKNet.EfCore.DtoGenerator.md#diagnostics--exceptions) |

## Triage shortcuts

- **Nothing generated at all, no diagnostic either**: check the DTO shell is `partial` and the `[GenerateDto]`/`[CrudCreate]` attribute namespaces are right (`DKNet.EfCore.DtoGenerator` / `DKNet.EfCore.Abstractions.Attributes`) — a wrong namespace is a plain `CS0246` from the C# compiler, not one of the IDs above.
- **"Duplicate type" build error naming a `Request`/`Handler`/`Map{Entity}Crud` type**: you hand-wrote something the generator also emits. Delete the hand-written type, or — for a handler only — implement `IHandler<GeneratedRequestName, TDto>` instead of redeclaring the request.
- **A generated endpoint/handler/request is missing and no error was reported**: check `DKCRUDGEN005`/`DKCRUDGEN010` (Info-level, easy to miss in a build log) before assuming the generator is broken — both mean "generation was intentionally skipped," not a failure.
- **Want to see the actual emitted `.g.cs`**: set `EmitCompilerGeneratedFiles`/`CompilerGeneratedFilesOutputPath` — see [SKILL.md § Inspect the code a generator emitted](../SKILL.md#inspect-the-code-a-generator-emitted).
