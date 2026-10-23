# Refuse a delete

Part of [DKNet.SlimBus.Generators](index.md). The DELETE route takes no request body and needs no attribute, so there was nothing to hang a rule on. The generator closes that gap by emitting, into `{Entity}CrudRequests.g.cs` alongside the create and update records, a delete request for **every** entity in the generated set:

```csharp
/// <summary>Delete request generated for Product, carrying the target's key bound from the route.</summary>
public sealed partial record DeleteProductRequest : global::DKNet.SlimBus.Extensions.Fluents.Requests.IWithKey<global::System.Guid>
{
    /// <summary>The target Product identifier (bound from route).</summary>
    public global::System.Guid Id { get; set; }
}
```

`Map{Entity}Crud` binds it on the DELETE route — `group.MapDeleteById<Product, Guid, DeleteProductRequest>()`, the three-type-argument overload in `DKNet.AspCore.Extensions`. Nothing else moves: no annotation or opt-in is needed, the route address and HTTP verb are unchanged, the request body is still never read, and there is **no handler** — the delete goes straight to `IRepositorySpec` through `MapDeleteById`, not through SlimBus.

![Every entity gets a Delete{Entity}Request bound to the three-argument MapDeleteById, unless a create/update/action member already resolves to that name, in which case DKCRUDGEN010 is reported and the route falls back to the request-less two-argument MapDeleteById.](diagrams/refuse-a-delete.svg)

To refuse a delete, validate the generated request and let the group's failure filter answer. With the `SharpGrip.FluentValidation.AutoValidation.Endpoints` pattern that is a 400 `ProblemDetails`:

```csharp
public sealed class DeleteProductRequestValidator : AbstractValidator<DeleteProductRequest>
{
    public DeleteProductRequestValidator(AppDbContext db) =>
        RuleFor(x => x.Id)
            .MustAsync((id, ct) => IsNotReferencedAsync(db, id, ct))
            .WithMessage("Product is still on an order and cannot be deleted.");

    private static async Task<bool> IsNotReferencedAsync(AppDbContext db, Guid productId, CancellationToken ct) =>
        !await db.OrderLines.AnyAsync(l => l.ProductId == productId, ct);
}
```

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<DeleteProductRequestValidator>();

var products = app.MapGroup("/products");
products.MapProductCrud();
products.AddFluentValidationAutoValidation();   // only groups that opt in consult the validator
```

A refused delete never reaches `SaveChanges`, so the row survives and no audit entry or domain event is raised for it. The rule is keyed on the request type, so a rule on `DeleteProductRequest` never fires for `DeleteOrderRequest`.

**Name collision.** If a `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` member already resolves to the name `Delete{Entity}Request` (for example a `[CrudUpdate] public void Delete()`), the generator emits neither the delete record nor the three-argument map call, and falls back to `MapDeleteById<{Entity}, {Key}>()`. That entity's DELETE route keeps working unchanged, but there is no delete request left to guard — rename the member if you need the rule. The generator reports the skip as `DKCRUDGEN010` (Info), naming the entity, the colliding member and the request name it took; Info never fails a build.

## 🧱 Where it fits

`MapDeleteById` and its two overloads live in [DKNet.AspCore.Extensions](../DKNet.AspCore.Extensions/index.md); the delete goes through [DKNet.EfCore.Specifications](../DKNet.EfCore.Specifications/index.md)'s `IRepositorySpec`, never through a generated handler. See [Read the generator's diagnostics](read-diagnostics.md) for `DKCRUDGEN010`'s severity and build effect.

## ⚠️ Gotchas & limits

- No handler is generated for the delete request — it carries the key for validation only, never dispatched through SlimBus.
- 204 on success, 404 when no row matches, 409 when the database rejects the delete — unchanged whether or not a validator is registered.
- The collision fallback is silent to the compiler (Info, not Error): a build that should fail loudly when a delete rule stops applying will not, by itself, flag it.
