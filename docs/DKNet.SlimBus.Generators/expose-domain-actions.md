# Expose domain actions

Part of [DKNet.SlimBus.Generators](index.md). A `[CrudAction]` method is a named operation on the entity at its own `{id}/{segment}` route — it never claims the plain `{id}` route, whatever verb it uses:

```csharp
[CrudAction("approval")]
public void Approve([Required] string approver) { /* … */ AddEvent<OrderApproved>(); }

[CrudAction(Verb = CrudActionVerb.Patch)]
public void Archive() { /* … */ }
```

Compared with `[CrudUpdate]` using `Verb = Put`: an update replaces state and, positionally, may claim `{id}`; an action is always a named segment and never positional. `Verb = Patch` only changes the advertised HTTP method — there is no partial-update or merge semantics behind it. Marking one member with both `[CrudUpdate]` and `[CrudAction]` is `DKCRUDGEN007`, and that member is emitted as neither.

![A [CrudAction] member's route segment comes from its explicit Route or a kebab-cased method name; its compile-time parameter count then picks MapActionById for a body or MapParameterlessActionById for none.](diagrams/expose-domain-actions.svg)

**An action with no parameters takes no request body.** The generator picks the mapper by the action method's compile-time parameter count. `Archive()` above takes none, so its route is registered with `MapParameterlessActionById` and dispatches on a call that carries no body and no `Content-Type` header at all:

```http
PATCH /v1/products/8f0c5a62-5f1f-4a1e-9f8f-0f4a2d3b7c10/archive HTTP/1.1
Host: localhost
```

`MapParameterlessActionById`'s delegate binds only `(IMessageBus bus, TKey id)` — nothing in its signature reads a request body, so the target id always comes from the route, never from anything posted. `Approve(string approver)` takes a parameter, so it is unaffected: it still maps through `MapActionById` and its request body still carries the action's arguments.

## ⚙️ Configuration reference

`CrudActionAttribute`:

| Member | Type | Default | Effect |
|---|---|---|---|
| `Route` (positional ctor arg) | `string?` | `null` → kebab-cased method name (`Archive` → `archive`) | Route segment appended after `{id}/`. |
| `Verb` | `CrudActionVerb` | `Post` | Registered HTTP verb: `Post`, `Put`, or `Patch`. There is no `Delete`. |
| `Name` | `string?` | `null` | Overrides the generated request type name. |

## 🧱 Where it fits

An action request is structurally identical to an update request — implementing `IWitResponse<TDto>` and `IWithKey<TKey>` — see [Generate CRUD requests](generate-crud-requests.md). Its route is registered by `Map{Entity}Crud` from [DKNet.AspCore.Extensions](../DKNet.AspCore.Extensions/index.md); see [Map CRUD endpoints](map-crud-endpoints.md) for `CrudOp.Action` exclusion and configuration.

## ⚠️ Gotchas & limits

- An action never lands on `{id}`, regardless of declaration order or verb — only `[CrudUpdate]` members compete for the plain by-id route.
- `Verb = Patch`/`Put` changes only the advertised HTTP method; there is no partial-update merge semantics behind either.
- `[CrudUpdate]` and `[CrudAction]` on the same member is `DKCRUDGEN007` — the member is emitted as neither an update nor an action.
