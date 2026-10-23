# Read the generator's diagnostics

Part of [DKNet.SlimBus.Generators](index.md). Every diagnostic shares the `DKNet.SlimBus.Generators` category and the `DKCRUDGEN0xx` id scheme. Only three ids stop an entity from emitting anything at all — the rest are scoped to the one member or route that triggered them, or are purely informational. The message column is the exact `messageFormat` from `CrudDiagnostics`, with `{0}`/`{1}`/… left as placeholders for the entity, member or request name the generator substitutes in:

| Id | Severity | Message | Effect |
|---|---|---|---|
| `DKCRUDGEN001` | Error | `Entity '{0}' has [CrudCreate]/[CrudUpdate] members but no [GenerateDto(typeof({0}))] DTO was found in the compiling project` | Entity is skipped. |
| `DKCRUDGEN002` | Error | `Entity '{0}' has more than one [GenerateDto(typeof({0}))] DTO in the compiling project` | Entity is skipped. |
| `DKCRUDGEN003` | Error | `Entity '{0}' has more than one member marked [CrudCreate]; only one is allowed` | The entity still emits, with no create member. |
| `DKCRUDGEN004` | Error | `Member '{0}.{1}' is marked [CrudCreate]/[CrudUpdate] but is not public` | That member is skipped; the entity still emits. The message always names `[CrudCreate]/[CrudUpdate]`, even when the non-public member is actually a `[CrudAction]`. |
| `DKCRUDGEN005` | Info | `A hand-written type implementing IHandler<{0}, ...> was found; the generated handler for '{0}' was skipped` | Build succeeds; reported at the hand-written type's location. |
| `DKCRUDGEN006` | Error | `Entity '{0}' has [CrudCreate]/[CrudUpdate] members but does not implement DKNet.EfCore.Abstractions.Entities.IEntity<TKey>` | Entity is skipped. |
| `DKCRUDGEN007` | Error | `Member '{0}.{1}' is marked both [CrudUpdate] and [CrudAction]; keep exactly one` | That member is emitted as neither. |
| `DKCRUDGEN008` | Error | `Entity '{0}' members '{1}' and '{2}' both resolve to route segment '{3}'; give one an explicit distinct segment` | Reported; both members still emit. |
| `DKCRUDGEN009` | Error | `Entity '{0}' has more than one route named '{1}'; a [CrudUpdate]/[CrudAction] member must not reuse a reserved name (GetById, GetList, Create, Delete) or another route's name` | Reported per colliding name. |
| `DKCRUDGEN010` | Info | `Entity '{0}' member '{1}' already claims the request name '{2}'; the generated delete request was skipped and the delete route falls back to the request-less MapDeleteById, so a delete rule registered for '{2}' never runs` | Build succeeds; DELETE route falls back to the two-argument `MapDeleteById<TEntity, TKey>()`. |

![DKCRUDGEN001/002/006 fail the build and skip the whole entity; DKCRUDGEN003/004/007/008/009 fail the build but only the offending member or route is affected; DKCRUDGEN005/010 are informational and never fail the build.](diagrams/read-diagnostics.svg)

## 🧱 Where it fits

001/002/006 are raised while resolving an entity's DTO and key — see [Generate CRUD requests](generate-crud-requests.md). 003/004/007/008/009 are raised per member or per route while building that entity's model, so a single bad member never takes down the rest of the entity's slice. 005 comes from [Generate handlers](generate-handlers.md); 010 comes from [Refuse a delete](refuse-a-delete.md).

## ⚠️ Gotchas & limits

- An `Error`-severity diagnostic other than 001/002/006 still fails the build, even though the entity itself would otherwise still emit — every id in this table is `isEnabledByDefault: true` and not suppressible through this generator's own surface.
- Only `DKCRUDGEN005` and `DKCRUDGEN010` are `Info`; every other id is an `Error` by default.
- Diagnostics carry `Location.None` except `DKCRUDGEN005`, which points at the hand-written override type — the others show up in the build log without a source-file squiggle.
- `DKCRUDGEN004`'s message text says `[CrudCreate]/[CrudUpdate]` regardless of which of the three attributes the non-public member actually carries.
