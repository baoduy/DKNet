# Name requests and routes

Part of [DKNet.SlimBus.Generators](index.md). Everything the generator names is derived, never configurable beyond the attribute's `Name` (or, for `[CrudAction]`, its `Route`):

| Thing | Rule | From the example below |
|---|---|---|
| Namespace | `{AssemblyName}.Crud`, or `Generated.Crud` when the compilation has no assembly name | `MyApi.Crud`, for a compiling project whose assembly is named `MyApi` |
| Requests file | `{Entity}CrudRequests.g.cs`, one per entity | `ProductCrudRequests.g.cs` |
| Handlers file | `{Entity}CrudHandlers.g.cs`; omitted entirely when every member has a hand-written handler | `ProductCrudHandlers.g.cs` |
| Endpoints file | `{Entity}CrudEndpoints.g.cs`; emitted only when the project references `DKNet.AspCore.Extensions` | `ProductCrudEndpoints.g.cs` |
| Create request | `Create{Entity}Request` for a constructor, `{Method}{Entity}Request` for a method; `Name` on the attribute overrides both | `CreateProductRequest` |
| Update / action request | `{Method}{Entity}Request`; `Name` overrides | `RenameProductRequest` |
| Delete request | `Delete{Entity}Request`, one per entity, not overridable — suppressed when another member already resolves to that name ([Refusing a delete](refuse-a-delete.md)) | `DeleteProductRequest` |
| Handler | the request name with a trailing `Request` replaced by `Handler` (appended when the name does not end in `Request`) | `RenameProductHandler` |
| By-id specification | `{Entity}ByIdCrudSpec`, `file`-scoped, one per handlers file | `ProductByIdCrudSpec` |
| Endpoint extension class | `{Entity}CrudEndpointExtensions` | `ProductCrudEndpointExtensions` |
| Endpoint method | `Map{Entity}Crud` | `MapProductCrud` |

Route segments:

| Member | Segment |
|---|---|
| First `[CrudUpdate]` in declaration order | `{id}` — the plain by-id PUT |
| Every later `[CrudUpdate]` | `{id}/{kebab-cased method name}` |
| `[CrudAction("segment")]` | `{id}/segment`, exactly as written |
| `[CrudAction]` with no segment | `{id}/{kebab-cased method name}` |

Kebab-casing inserts a `-` before **every** upper-case character after the first and lower-cases it, so `UpdatePrice` becomes `update-price` and an acronym like `ExportXml` becomes `export-xml` — but `ExportXML` becomes `export-x-m-l`. Two members resolving to the same segment is `DKCRUDGEN008`.

Registration order inside `Map{Entity}Crud` is fixed: `GetById`, `GetList`, `Delete`, `Create`, each `[CrudUpdate]` in declaration order, then each `[CrudAction]` in declaration order.

Route names:

Every registered route also carries a **name**, separate from its segment. The name is what `CrudMapOptions.Configure(string, …)` matches on, and it is never kebab-cased, never the generated request record's name, and never changed by `Name` on `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` or by an explicit `[CrudAction]` segment:

| Route | Name |
|---|---|
| GET by id | `GetById` |
| GET list | `GetList` |
| POST create | `Create` |
| DELETE by id | `Delete` |
| Each `[CrudUpdate]` member | its C# method name, verbatim |
| Each `[CrudAction]` member | its C# method name, verbatim |

The four fixed routes are named exactly as their `CrudOp` members are spelled. Names are compared **ordinally** and must be unique within an entity; two routes of one entity resolving to the same name is `DKCRUDGEN009`. That needs a deliberate collision — a `[CrudUpdate]`/`[CrudAction]` method called `GetById`, `GetList`, `Create` or `Delete`, or two attributed overloads of one method name kept apart only by explicit route segments.

**Worked example — every route `Product` publishes:**

```csharp
using System;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.DtoGenerator;

namespace MyDomain
{
    public class Product : IEntity<Guid>
    {
        [CrudCreate]
        public Product(string name, decimal price)
        {
            Name = name;
            Price = price;
        }

        [CrudUpdate]
        public void Rename(string name) => Name = name;

        [CrudUpdate]
        public void ChangePrice(decimal price) => Price = price;

        public Guid Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public decimal Price { get; private set; }
    }
}

namespace MyApi
{
    [GenerateDto(typeof(MyDomain.Product))]
    public partial record ProductDto;
}
```

That entity publishes six routes, and these are all of them:

| Name | HTTP method | Segment |
|---|---|---|
| `GetById` | GET | `{id}` |
| `GetList` | GET | `/` |
| `Create` | POST | `/` |
| `Delete` | DELETE | `{id}` |
| `Rename` | PUT | `{id}` |
| `ChangePrice` | PUT | `{id}/change-price` |

```text
GetById
GetList
Create
Delete
Rename
ChangePrice
```

`Rename` is declared first, so it keeps the plain `{id}` PUT and `ChangePrice` gets the kebab-cased segment — but neither name is kebab-cased, and `ChangePrice` is the name whether or not it is the route that moved. `GetById` and `Delete` share the `{id}` segment under different verbs and still have distinct names: a name identifies a route, not a segment. `Product` declares no `[CrudAction]`, so it publishes no action routes.

![A [CrudUpdate]/[CrudAction] member derives three independent names at once: the request type name, the route segment, and the route name — renaming or re-segmenting one never touches the others.](diagrams/name-requests-and-routes.svg)

## 🧱 Where it fits

`Map{Entity}Crud`, generated only when the project references [DKNet.AspCore.Extensions](../DKNet.AspCore.Extensions/index.md), is what turns these names and segments into live routes — see [Map CRUD endpoints](map-crud-endpoints.md) for the registration and exclusion mechanics, and `CrudMapOptions.Configure`/`Exclude` for consuming a route's name at the call site.

## ⚠️ Gotchas & limits

- A route's **name** and its **segment** are independent: `ChangePrice` always has that name, whether or not it is the member that keeps the plain `{id}` route.
- Kebab-casing an acronym is not special-cased — `ExportXML` becomes `export-x-m-l`, not `export-xml`.
- Renaming a request type (`[CrudUpdate(Name = "...")]`) changes the request record's name and its handler's name, but never the route's name used by `CrudMapOptions`.
- The four fixed route names (`GetById`, `GetList`, `Create`, `Delete`) are reserved: a `[CrudUpdate]`/`[CrudAction]` method sharing one of them is `DKCRUDGEN009`.
