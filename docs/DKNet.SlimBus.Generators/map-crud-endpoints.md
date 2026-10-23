# Map CRUD endpoints

Part of [DKNet.SlimBus.Generators](index.md). `{Entity}CrudEndpoints.g.cs` is generated only when the compiling project references [DKNet.AspCore.Extensions](../DKNet.AspCore.Extensions/index.md) — a project that only wants requests/handlers (no ASP.NET Core dependency) gets exactly that, with no file full of unresolved types forced on it. When it is generated, it composes the **existing** `DKNet.AspCore.Extensions` mappers behind one call:

```csharp
app.MapGroup("/products").MapProductCrud();
```

```csharp
// By operation kind — no DELETE route is registered at all.
app.MapGroup("/products").MapProductCrud(o => o.Exclude(CrudOp.Delete));

// By route name — `ChangePrice` is withdrawn, `Rename` keeps the plain `{id}` PUT.
app.MapGroup("/products").MapProductCrud(o => o.Exclude("ChangePrice"));
```

![Map{Entity}Crud validates every route name the entity has up front, then for each operation checks both its CrudOp and its route-name exclusion before mapping, and applies operation-kind settings before name settings.](diagrams/map-crud-endpoints.svg)

`Map{Entity}Crud(this RouteGroupBuilder, Action<CrudMapOptions>? configure = null)` registers:

| Operation | Route | Response |
|---|---|---|
| GET by id | `{id}` | 200 + DTO / 404 |
| GET list (paged) | `/` | 200 |
| POST create | `/` | 201 + DTO body (`Location` header is the placeholder `/`) |
| PUT — first `[CrudUpdate]` | `{id}` | 200 + DTO body / 404 |
| PUT — additional `[CrudUpdate]`s | `{id}/{kebab-case-method-name}` | 200 + DTO body / 404 |
| `[CrudAction]` (POST/PUT/PATCH) | `{id}/{segment}` | 200 + DTO body / 404 |
| DELETE by id | `{id}` | 204 / 404, or 409 when the database rejects the delete |

`ValidateRouteNames` is emitted with every route name the entity has and runs before anything is mapped, so a name `Exclude`/`Configure` passed that the entity does not have throws `ArgumentException` there. Each registration is then behind both guards — `Exclude(CrudOp.Update)` drops both PUTs, `Exclude("Rename")` drops only the second — and `options.Apply` is what runs a route's settings, operation-kind first, name second. Naming a route whose operation is excluded is not an error: the name is still validated, and the setting is then silently dropped along with the route.

## ⚙️ Configuration reference

`CrudMapOptions` is declared in [DKNet.AspCore.Extensions](../DKNet.AspCore.Extensions/index.md), which owns its full member reference: see [Configure generated CRUD routes](../DKNet.AspCore.Extensions/configure-crud-routes.md). This generator only decides which route each `Exclude`/`Configure` call reaches — by the names and segments in [Name requests and routes](name-requests-and-routes.md).

## 🧱 Where it fits

`Map{Entity}Crud` composes `MapGetById`, `MapGetList`, `MapDeleteById`, `MapPost`, `MapPutById`, `MapActionById` and `MapParameterlessActionById` from [DKNet.AspCore.Extensions](../DKNet.AspCore.Extensions/index.md) — no new mapping layer. It never runs without that package referenced; see [Generate CRUD requests](generate-crud-requests.md) for what exists without it.

## ⚠️ Gotchas & limits

- **`201 Created` depends on the request type's name.** `MapPost` returns `201` only when the request type name contains "Create" (case-insensitive) — true for the default `Create{Entity}Request`, and easy to lose by setting `Name` to something else, which silently downgrades the response to `200`.
- **The `Location` header on create is the placeholder `/`.** It is not the created resource's URL; don't build a client around following it.
- **A misspelt `Exclude`/`Configure` name throws at registration**, not silently — that is the safety net for "my authorization silently vanished" and for "the route I withdrew is still live".
