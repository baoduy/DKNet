# Generate handlers, unless a hand-written one wins

Part of [DKNet.SlimBus.Generators](index.md). Each request gets an `internal sealed IHandler<TRequest, TDto>` in `{Entity}CrudHandlers.g.cs` — unless the compiling project already declares a type implementing `IHandler<TRequest, TDto>` for that request, in which case the generator skips it and reports `DKCRUDGEN005` (Info) at the hand-written type. The request record itself is still generated either way. From `SlimBus.Generators.Tests.Api`, a passing-test override of a generated action handler:

```csharp
internal sealed class CustomDiscontinueGadgetHandler(GadgetDbContext db, IMapper mapper)
    : Fluents.Requests.IHandler<DiscontinueGadgetRequest, GadgetDto>
{
    public async Task<IResult<GadgetDto>> OnHandle(DiscontinueGadgetRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.Gadgets.FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);
        if (entity is null)
            return Result.Fail<GadgetDto>(new NotFoundError($"Gadget '{request.Id}' was not found (custom handler)."));

        return Result.Ok(mapper.Map<GadgetDto>(entity));
    }
}
```

![The generator looks up a hand-written IHandler<TRequest,...> matching the request's type name; a match reports DKCRUDGEN005 and the generated handler is skipped, otherwise a generated handler is emitted.](diagrams/generate-handlers.svg)

**Create** — always calls the `[CrudCreate]` member's **constructor** with the request's values (never the marked member itself when it is a method — see [Generate CRUD requests](generate-crud-requests.md)), then `IRepositorySpec.AddAsync`. Verbatim, from `obj/.../generated/.../ProductCrudHandlers.g.cs` after compiling the [Quick Start](index.md#-quick-start) project:

```csharp
/// <summary>Generated create handler for Product. Write a class implementing the same IHandler to replace it.</summary>
internal sealed class CreateProductHandler(
    global::DKNet.EfCore.Specifications.Repositories.IRepositorySpec repository,
    global::MapsterMapper.IMapper mapper)
    : global::DKNet.SlimBus.Extensions.Fluents.Requests.IHandler<CreateProductRequest, global::Api.ProductDto>
{
    /// <inheritdoc />
    public async global::System.Threading.Tasks.Task<global::FluentResults.IResult<global::Api.ProductDto>> OnHandle(
        CreateProductRequest request, global::System.Threading.CancellationToken cancellationToken)
    {
        var entity = new global::Catalog.Product(request.Name, request.Price);
        await repository.AddAsync(entity, cancellationToken);
        return global::DKNet.SlimBus.Extensions.LazyMapper.LazyMapExtensions.ResultOf<global::Api.ProductDto>(mapper, entity);
    }
}
```

**Update / action** — fetches the entity through a generated, `file`-scoped `{Entity}ByIdCrudSpec`; a miss returns `Result.Fail` wrapping `NotFoundError` (the endpoint layer maps that to `404`), a hit invokes the marked method:

```csharp
/// <summary>Matches a single Product by id; used by this file's generated update handlers to fetch the entity.</summary>
file sealed class ProductByIdCrudSpec : global::DKNet.EfCore.Specifications.Definitions.Specification<global::Catalog.Product>
{
    public ProductByIdCrudSpec(global::System.Guid id) => WithFilter(x => x.Id.Equals(id));
}

internal sealed class UpdatePriceProductHandler(
    global::DKNet.EfCore.Specifications.Repositories.IRepositorySpec repository,
    global::MapsterMapper.IMapper mapper)
    : global::DKNet.SlimBus.Extensions.Fluents.Requests.IHandler<UpdatePriceProductRequest, global::Api.ProductDto>
{
    public async global::System.Threading.Tasks.Task<global::FluentResults.IResult<global::Api.ProductDto>> OnHandle(
        UpdatePriceProductRequest request, global::System.Threading.CancellationToken cancellationToken)
    {
        var entity = await repository.FirstOrDefaultAsync(new ProductByIdCrudSpec(request.Id), cancellationToken);
        if (entity is null)
            return global::FluentResults.Result.Fail<global::Api.ProductDto>(
                new global::DKNet.SlimBus.Extensions.NotFoundError($"Product '{request.Id}' was not found."));
        entity.UpdatePrice(request.Price);
        return global::DKNet.SlimBus.Extensions.LazyMapper.LazyMapExtensions.ResultOf<global::Api.ProductDto>(mapper, entity);
    }
}
```

Both handler kinds return `mapper.ResultOf<TDto>(entity)` (via `LazyMapExtensions`), so the DTO materializes lazily — after the auto-save interceptor has persisted the change, which is why database-generated values (identity keys, timestamps) are present in the response.

## 🧱 Where it fits

Generated handlers call `IRepositorySpec` from [DKNet.EfCore.Specifications](../DKNet.EfCore.Specifications/index.md), `NotFoundError` and `LazyMapExtensions.ResultOf` from [DKNet.SlimBus.Extensions](../DKNet.SlimBus.Extensions/index.md), and a registered Mapster `IMapper`. They never call `SaveChangesAsync`: [DKNet.SlimBus.Extensions](../DKNet.SlimBus.Extensions/index.md)'s auto-save interceptor (`AddSlimBusEfCoreInterceptor<TDbContext>()`, see [Auto-save on write](../DKNet.SlimBus.Extensions/auto-save-dbcontext.md)) does that after a successful dispatch, and that same save is what makes the entity's domain events reach [`AddSlimBusEventPublisher<TDbContext>()`](../DKNet.SlimBus.Extensions/publish-domain-events.md) — nothing in this generator's output raises, declares, or subscribes to an event itself.

## ⚠️ Gotchas & limits

- **Matching ignores types.** Only the request type's *name* is compared against a hand-written `IHandler<,>`'s first type argument, so a hand-written handler with the wrong DTO type argument silently replaces the generated one and fails at dispatch instead of at build.
- **`{Entity}CrudHandlers.g.cs` is omitted entirely** when every one of an entity's create/update/action members already has a hand-written handler — there is nothing left for the file to contain.
- No generated handler ever calls `SaveChangesAsync`; without the auto-save interceptor wired up, a generated write never persists.
