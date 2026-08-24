# CRUD Vertical-Slice Generation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A developer marks entity ctors/methods with `[CrudCreate]`/`[CrudUpdate]`; a new Roslyn generator emits SlimBus request records, handlers returning the entity's DTO, and a one-line endpoint registration — everything else reuses existing DKNet generic mappers.

**Architecture:** Hybrid — small runtime additions (LazyMapper, `NotFoundError`, `IWithKey`/`MapPutById`, `CrudMapOptions`) plus a new incremental source generator `DKNet.SlimBus.Generators` hosted by the API-layer project, scanning referenced assemblies for marked members and the current compilation for the `[GenerateDto]` DTO.

**Tech Stack:** .NET 10 solution (generator itself netstandard2.0, Roslyn 4.11.0 pinned), SlimMessageBus, FluentResults, Mapster, xUnit + Shouldly, SQLite for endpoint slice tests.

**Spec:** `docs/superpowers/specs/2026-08-24-crud-vertical-slice-generation-design.md`

## Global Constraints

- Run all dotnet commands from `src/` (solution `DKNet.FW.sln`).
- `TreatWarningsAsErrors=true` + `GenerateDocumentationFile=true` solution-wide: every new public API needs full XML docs (`<summary>`, `<param>`, `<returns>`); zero warnings.
- Every new `.cs` file starts with the 5-line copyright header (copy the exact style from `src/AspNet/DKNet.AspCore.Extensions/Responses/ResultResponseExtensions.cs:1-5`, adjusting `File:` and `Description:`).
- NuGet versions only in `src/Directory.Packages.props` (central package management).
- Test naming `MethodName_Scenario_ExpectedBehavior`; xUnit + Shouldly; no EF InMemory provider.
- Never run TestContainers.MsSql tests locally (ARM host). This plan's tests use plain unit tests, Roslyn in-memory compilations, and SQLite — all runnable locally. Full-solution verification goes through `gh workflow run remote-tests.yml`.
- Conventional Commits with scope (`slimbus`, `abstractions`, `aspcore-extensions`, `generators`, `tests`, `docs`).
- Folder-per-concern: folder name = last namespace segment.
- `dotnet format <project>` before each commit.
- Work on branch `feat/crud-generation` off `dev` (Task 1 creates it).

---

### Task 1: Branch + throwaway spike — cross-assembly attribute scan

**Files:**
- Create (throwaway, in scratchpad — NOT in the repo): a two-project mini solution + one generator project.

**Interfaces:**
- Consumes: nothing.
- Produces: a go/no-go finding recorded in the final report: can an `IIncrementalGenerator` using `context.CompilationProvider` find `[CrudCreate]`-attributed members on a type defined in a *referenced project*, and emit source into the host project? (Expected: yes via `compilation.SourceModule.ReferencedAssemblySymbols`.)

- [ ] **Step 1: Create branch**

```bash
cd /Users/steven/_CODE/GIT/DKNet && git checkout -b feat/crud-generation dev
```

- [ ] **Step 2: Build the spike in the scratchpad directory** (not the repo)

Create `spike/Domain` (classlib, net10.0) with:

```csharp
namespace Domain;
[System.AttributeUsage(System.AttributeTargets.Constructor | System.AttributeTargets.Method)]
public sealed class CrudCreateAttribute : System.Attribute;

public class Product
{
    [CrudCreate]
    public Product(string name) => Name = name;
    public string Name { get; private set; }
}
```

Create `spike/Gen` (classlib, netstandard2.0, `<IsRoslynComponent>true</IsRoslynComponent>`, PackageReference `Microsoft.CodeAnalysis.CSharp` 4.11.0 PrivateAssets=all) with an `IIncrementalGenerator` that:

```csharp
var provider = context.CompilationProvider.Select((compilation, _) =>
{
    var found = new List<string>();
    foreach (var asm in compilation.SourceModule.ReferencedAssemblySymbols)
        foreach (var type in GetAllTypes(asm.GlobalNamespace))
            foreach (var member in type.GetMembers())
                if (member.GetAttributes().Any(a => a.AttributeClass?.Name == "CrudCreateAttribute"))
                    found.Add($"{type.Name}.{member.Name}");
    return found.ToImmutableArray();
});
context.RegisterSourceOutput(provider, (spc, found) =>
    spc.AddSource("Spike.g.cs", $"// found: {string.Join(",", found)}\npublic static class SpikeMarker {{ public const string Found = \"{string.Join(",", found)}\"; }}"));
```

(`GetAllTypes` = recursive walk of `INamespaceSymbol.GetMembers()`.)

Create `spike/Api` (classlib, net10.0) referencing `Domain` (ProjectReference) and `Gen` (ProjectReference with `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`), containing `class Probe { const string X = SpikeMarker.Found; }`.

- [ ] **Step 3: Build and verify**

Run: `dotnet build spike/Api` — Expected: builds, and `SpikeMarker.Found` contains `Product..ctor` (i.e. the referenced-assembly member was found). If it fails, record exactly what Roslyn returned and STOP — escalate to the user before continuing, because the whole architecture depends on this.

- [ ] **Step 4: Record the finding and delete the spike**

Delete the scratchpad spike folder. Nothing is committed. Report the finding (works / doesn't, plus any nuance like needing `ReferencedAssemblySymbols` vs `GetTypeByMetadataName`).

---

### Task 2: Port LazyMapper into DKNet.SlimBus.Extensions

**Files:**
- Create: `src/SlimBus/DKNet.SlimBus.Extensions/LazyMapper/LazyMap.cs`
- Create: `src/SlimBus/DKNet.SlimBus.Extensions/LazyMapper/LazyResult.cs`
- Create: `src/SlimBus/DKNet.SlimBus.Extensions/LazyMapper/LazyMapExtensions.cs`
- Modify: `src/SlimBus/DKNet.SlimBus.Extensions/DKNet.SlimBus.Extensions.csproj` (add `<PackageReference Include="MapsterMapper"/>`)
- Modify: `src/Directory.Packages.props` ONLY IF `MapsterMapper` has no `PackageVersion` entry yet (check first — Mapster is already used by EfCore.Repos, the entry likely exists).
- Test: `src/SlimBus/SlimBus.Extensions.Tests/LazyMapperTests.cs`

**Interfaces:**
- Consumes: `MapsterMapper.IMapper`, `FluentResults.IResult<T>`.
- Produces (used by Task 7's generated handlers and Task 9):
  - `namespace DKNet.SlimBus.Extensions.LazyMapper`
  - `public interface ILazyMap<out TResult> { TResult Value { get; } TResult? ValueOrDefault { get; } }`
  - `public static class LazyMapExtensions` with `public static ILazyMap<TValue> LazyMap<TValue>(this IMapper mapper, object value)` and `public static IResult<TValue> ResultOf<TValue>(this IMapper mapper, object value)`

- [ ] **Step 1: Write failing tests**

Port source is `/Users/steven/_CODE/GIT/DKNet.Templates/src/ApiEndpoints/Minimal.AppServices/Extensions/LazyMapper/` (3 files — read them). Tests (follow existing test style in `SlimBus.Extensions.Tests`, xUnit + Shouldly):

```csharp
public class LazyMapperTests
{
    private static IMapper NewMapper() => new Mapper(new TypeAdapterConfig());

    private sealed record Source(string Name);
    private sealed record Target { public string Name { get; init; } = ""; }

    [Fact]
    public void LazyMap_WithDifferentType_MapsViaMapster()
        => NewMapper().LazyMap<Target>(new Source("a")).Value.Name.ShouldBe("a");

    [Fact]
    public void LazyMap_WithSameType_ReturnsSameInstance()
    { var s = new Source("a"); NewMapper().LazyMap<Source>(s).Value.ShouldBeSameAs(s); }

    [Fact]
    public void LazyMap_WithNullValue_ValueThrows_AndValueOrDefaultIsNull()
    {
        var lazy = NewMapper().LazyMap<Target>(null!);
        lazy.ValueOrDefault.ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => _ = lazy.Value);
    }

    [Fact]
    public void ResultOf_WithValue_IsSuccessAndMapsValue()
    {
        var rs = NewMapper().ResultOf<Target>(new Source("a"));
        rs.IsSuccess.ShouldBeTrue();
        rs.Value.Name.ShouldBe("a");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SlimBus.Extensions.Tests --filter "FullyQualifiedName~LazyMapperTests"` — Expected: compile failure (types don't exist).

- [ ] **Step 3: Port the three files**

Copy the template's `LazyMap.cs`, `LazyResult.cs`, `LazyMapExtensions.cs` into `LazyMapper/`, changing namespace to `DKNet.SlimBus.Extensions.LazyMapper`, adding the copyright header, full XML docs on all public members (`ILazyMap<TResult>`, `LazyMapExtensions` and its two methods), and `using FluentResults; using MapsterMapper;`. Keep `LazyMap<TResult>` and `LazyResult<TResult>` internal exactly as in the template. Add `<PackageReference Include="MapsterMapper"/>` to the csproj (version comes from `Directory.Packages.props` — add `<PackageVersion Include="MapsterMapper" Version="..."/>` there only if missing, matching the Mapster line's version family).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test SlimBus.Extensions.Tests --filter "FullyQualifiedName~LazyMapperTests"` — Expected: 4 PASS. Also `dotnet build DKNet.FW.sln -c Debug` → zero warnings.

- [ ] **Step 5: Commit**

```bash
dotnet format DKNet.SlimBus.Extensions && git add -A && git commit -m "feat(slimbus): port LazyMapper (ILazyMap, LazyResult, ResultOf) from templates"
```

---

### Task 3: NotFoundError + HTTP 404 mapping

**Files:**
- Create: `src/SlimBus/DKNet.SlimBus.Extensions/NotFoundError.cs`
- Modify: `src/AspNet/DKNet.AspCore.Extensions/Responses/ProblemDetailsExtensions.cs:53-68` (`ToProblemDetails(IResultBase, ...)`)
- Test: `src/AspNet/AspCore.Extensions.Tests/Responses/NotFoundErrorResponseTests.cs`

**Interfaces:**
- Consumes: `FluentResults.Error`.
- Produces (used by Task 7's generated update handler):
  - `namespace DKNet.SlimBus.Extensions; public sealed class NotFoundError : Error { public NotFoundError(string message) : base(message) { } }`
  - Behavior: `Result.Fail(new NotFoundError("...")).Response()` → HTTP 404 ProblemDetails.

- [ ] **Step 1: Write failing tests**

```csharp
public class NotFoundErrorResponseTests
{
    [Fact]
    public void ToProblemDetails_WithNotFoundError_Returns404Status()
    {
        var pd = Result.Fail(new NotFoundError("Product abc not found")).ToProblemDetails();
        pd.ShouldNotBeNull();
        pd.Status.ShouldBe(StatusCodes.Status404NotFound);
        pd.Detail.ShouldBe("Product abc not found");
    }

    [Fact]
    public void ToProblemDetails_WithPlainError_Returns400Status()
    {
        var pd = Result.Fail(new Error("boom")).ToProblemDetails();
        pd.ShouldNotBeNull();
        pd.Status.ShouldBe(StatusCodes.Status400BadRequest);
    }
}
```

- [ ] **Step 2: Run to verify failure** — `dotnet test AspCore.Extensions.Tests --filter "FullyQualifiedName~NotFoundErrorResponseTests"` → compile failure.

- [ ] **Step 3: Implement**

`NotFoundError.cs` (header + XML docs) as in Interfaces above. In `ToProblemDetails(this IResultBase result, HttpStatusCode statusCode = HttpStatusCode.BadRequest)` add, right after the `IsSuccess` early return:

```csharp
if (result.Errors.Any(e => e is NotFoundError))
    statusCode = HttpStatusCode.NotFound;
```

`DKNet.AspCore.Extensions` already references `DKNet.SlimBus.Extensions` (it uses `Fluents` in the endpoint mappers) — add `using DKNet.SlimBus.Extensions;` to the file. Update the method's XML doc `<param name="statusCode">` to mention the NotFoundError override.

- [ ] **Step 4: Run tests** — both new tests PASS; run the whole `AspCore.Extensions.Tests` project locally to catch regressions in existing Response tests. Expected: all pass.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat(aspcore-extensions): map NotFoundError results to HTTP 404 problem details"`

---

### Task 4: [CrudCreate]/[CrudUpdate] attributes in DKNet.EfCore.Abstractions

**Files:**
- Create: `src/EfCore/DKNet.EfCore.Abstractions/Attributes/CrudCreateAttribute.cs`
- Create: `src/EfCore/DKNet.EfCore.Abstractions/Attributes/CrudUpdateAttribute.cs`
- Test: `src/EfCore/EfCore.Abstractions.Tests/CrudAttributeTests.cs` (check the test project name on disk first — it sits next to `DKNet.EfCore.Abstractions`; if the existing tests live under a differently named project, put the file there.)

**Interfaces:**
- Consumes: nothing.
- Produces (read by the Task 6 generator **by metadata name** — these exact full names are load-bearing):
  - `DKNet.EfCore.Abstractions.Attributes.CrudCreateAttribute` — `[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method, AllowMultiple = false)]`, `public string? Name { get; set; }`
  - `DKNet.EfCore.Abstractions.Attributes.CrudUpdateAttribute` — `[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]`, `public string? Name { get; set; }`

- [ ] **Step 1: Check existing pattern** — look at an existing attribute in `src/EfCore/DKNet.EfCore.Abstractions/Attributes/` (e.g. the AuditLog one) and mirror its file layout/namespace (`DKNet.EfCore.Abstractions.Attributes`).

- [ ] **Step 2: Write failing test**

```csharp
public class CrudAttributeTests
{
    [Fact]
    public void CrudCreateAttribute_Usage_AllowsConstructorAndMethodOnly()
    {
        var usage = typeof(CrudCreateAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        usage.ValidOn.ShouldBe(AttributeTargets.Constructor | AttributeTargets.Method);
        usage.AllowMultiple.ShouldBeFalse();
    }

    [Fact]
    public void CrudUpdateAttribute_Usage_AllowsMethodOnly()
    {
        var usage = typeof(CrudUpdateAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        usage.ValidOn.ShouldBe(AttributeTargets.Method);
        usage.AllowMultiple.ShouldBeFalse();
    }
}
```

- [ ] **Step 3: Run to verify failure**, then implement both attributes (sealed, XML docs explaining: "marks the member whose parameters become the generated Create/Update request; the optional Name overrides the generated request type name").

- [ ] **Step 4: Run tests to verify pass**, plus `dotnet build DKNet.FW.sln -c Debug` → zero warnings.

- [ ] **Step 5: Commit** — `git commit -m "feat(abstractions): add CrudCreate/CrudUpdate marker attributes for CRUD generation"`

---

### Task 5: Fluents.Requests.IWithKey + MapPutById

**Files:**
- Modify: `src/SlimBus/DKNet.SlimBus.Extensions/Fluents.cs` (add nested interface inside `Fluents.Requests`)
- Modify: `src/AspNet/DKNet.AspCore.Extensions/Endpoints/FluentEndpointMapperExtensions.cs` (add `MapPutById` inside the `extension(RouteGroupBuilder app)` block)
- Test: `src/AspNet/AspCore.Extensions.Tests/Endpoints/MapPutByIdTests.cs`

**Interfaces:**
- Consumes: `Fluents.Requests.IWitResponse<TResponse>`, `ResultResponseExtensions.Response`, existing test host fixture (`AspCore.Extensions.Tests/Fixtures/EndpointTestHost.cs` — read it and follow how other endpoint tests register routes/handlers).
- Produces (called by Task 8's generated extension):
  - `Fluents.Requests.IWithKey<TKey> { TKey Id { get; set; } }`
  - `RouteHandlerBuilder MapPutById<TCommand, TKey, TResponse>(string endpoint = "{id}") where TCommand : class, Fluents.Requests.IWitResponse<TResponse>, Fluents.Requests.IWithKey<TKey>` — binds `TCommand` from body, `TKey id` from route, assigns `request.Id = id` before `bus.Send`, returns `rs.Response()`, `.Produces<TResponse>().ProducesCommons()`.

- [ ] **Step 1: Write failing test** — in the existing endpoint-test style (WebApplicationFactory/test host + fake bus or real SlimBus registration; mirror how `MapPost`/`MapPut` are already tested in this project — grep `MapPut` in `AspCore.Extensions.Tests` first and copy that harness). Assert:
  - PUT `/things/{guid}` with body `{"name":"x"}` reaches the handler with `request.Id` == the route guid (route wins even if body contains a different Id).
  - Success returns 200 with the response payload.

Test request/handler fixture:

```csharp
public sealed record RenameThingRequest : Fluents.Requests.IWitResponse<string>, Fluents.Requests.IWithKey<Guid>
{
    public Guid Id { get; set; }
    public string Name { get; init; } = "";
}
internal sealed class RenameThingHandler : Fluents.Requests.IHandler<RenameThingRequest, string>
{
    public Task<IResult<string>> OnHandle(RenameThingRequest request, CancellationToken cancellationToken)
        => Task.FromResult<IResult<string>>(Result.Ok($"{request.Id}:{request.Name}"));
}
```

- [ ] **Step 2: Run to verify failure** — compile failure on `IWithKey`/`MapPutById`.

- [ ] **Step 3: Implement**

In `Fluents.Requests` add (with XML docs):

```csharp
/// <summary>Represents a request carrying the target entity's key, populated from the route by MapPutById.</summary>
/// <typeparam name="TKey">The entity key type.</typeparam>
public interface IWithKey<TKey>
{
    /// <summary>The target entity's identifier.</summary>
    TKey Id { get; set; }
}
```

In `FluentsEndpointMapperExtensions` (inside the extension block, XML docs matching siblings):

```csharp
public RouteHandlerBuilder MapPutById<TCommand, TKey, TResponse>(string endpoint = "{id}")
    where TCommand : class, Fluents.Requests.IWitResponse<TResponse>, Fluents.Requests.IWithKey<TKey>
{
    return app.MapPut(
            endpoint,
            async (IMessageBus bus, TKey id, TCommand request) =>
            {
                request.Id = id;
                var rs = await bus.Send(request);
                return rs.Response();
            }).Produces<TResponse>()
        .ProducesCommons();
}
```

- [ ] **Step 4: Run tests** — `dotnet test AspCore.Extensions.Tests --filter "FullyQualifiedName~MapPutByIdTests"` → PASS.

- [ ] **Step 5: Commit** — `git commit -m "feat(aspcore-extensions): add MapPutById binding route key into IWithKey requests"`

---

### Task 6: Generator project — discovery, DTO resolution, request-record emission

**Files:**
- Create: `src/SlimBus/DKNet.SlimBus.Generators/DKNet.SlimBus.Generators.csproj`
- Create: `src/SlimBus/DKNet.SlimBus.Generators/CrudGenerator.cs` (IIncrementalGenerator: discovery + diagnostics + request emission this task; handler/endpoint emission arrive in Tasks 7–8)
- Create: `src/SlimBus/DKNet.SlimBus.Generators/CrudModels.cs` (value-equatable model records)
- Create: `src/SlimBus/DKNet.SlimBus.Generators/README.md`
- Create: `src/SlimBus/SlimBus.Generators.Tests/SlimBus.Generators.Tests.csproj`
- Create: `src/SlimBus/SlimBus.Generators.Tests/GeneratorTestHelper.cs`
- Create: `src/SlimBus/SlimBus.Generators.Tests/RequestEmissionTests.cs`
- Create: `src/SlimBus/SlimBus.Generators.Tests/DiagnosticTests.cs`
- Modify: `src/DKNet.FW.sln` (add both projects: `dotnet sln DKNet.FW.sln add SlimBus/DKNet.SlimBus.Generators SlimBus/SlimBus.Generators.Tests`)

**Interfaces:**
- Consumes: attribute metadata names from Task 4; `[GenerateDto]` attribute name `DKNet.EfCore.Abstractions.Attributes` — verify: read `src/EfCore/DKNet.EfCore.DtoGenerator/GenerateDtoAttribute.cs` for its exact namespace and match on that full metadata name.
- Produces:
  - Generated file per entity: `{EntityName}CrudRequests.g.cs`, namespace `{compilation.AssemblyName}.Crud`.
  - Model records in `CrudModels.cs` used by Tasks 7–8: `CrudEntityModel(string EntityFullName, string EntityName, string KeyFullName, string DtoFullName, string DtoName, CrudMemberModel? Create, ImmutableArray<CrudMemberModel> Updates)`; `CrudMemberModel(string RequestName, string MemberName, bool IsConstructor, ImmutableArray<CrudParamModel> Params)`; `CrudParamModel(string Name, string PascalName, string TypeFullName, ImmutableArray<string> AnnotationSources)` — all `readonly record struct`/`sealed record` with value-equal collections (use `ImmutableArray` + custom `Equals` or `EquatableArray` helper, mirroring how `DtoGenerator.cs` handles equality; read it before writing this).
  - Diagnostics: `DKCRUDGEN001` no `[GenerateDto(typeof(Entity))]` DTO found in compiling project (Error); `DKCRUDGEN002` multiple DTOs found (Error); `DKCRUDGEN003` more than one `[CrudCreate]` on an entity (Error); `DKCRUDGEN004` marked member not public (Error); `DKCRUDGEN005` hand-written handler exists, generated handler skipped (Info — emitted by Task 7); `DKCRUDGEN006` entity does not implement `IEntity<TKey>` (Error).

**Generator pipeline (the shape that survived the Task 1 spike):**

```csharp
[Generator]
public sealed class CrudGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.CompilationProvider.Select(static (compilation, ct) =>
            CrudModelBuilder.Build(compilation, ct)); // returns EquatableArray<CrudEntityModel> + diagnostics
        context.RegisterSourceOutput(models, static (spc, result) => Emitter.Emit(spc, result));
    }
}
```

`CrudModelBuilder.Build`:
1. Walk `compilation.Assembly` **and** `compilation.SourceModule.ReferencedAssemblySymbols` (skip assemblies whose name starts with `System`, `Microsoft`, `netstandard`, `mscorlib` — cheap filter), recursing namespaces for named types.
2. For each type, collect members carrying `CrudCreateAttribute`/`CrudUpdateAttribute` (match by `AttributeClass` full metadata name `DKNet.EfCore.Abstractions.Attributes.CrudCreateAttribute` / `...CrudUpdateAttribute`).
3. For a hit type: resolve `TKey` from its `DKNet.EfCore.Abstractions.Entities.IEntity<TKey>` implementation (`AllInterfaces`); missing → `DKCRUDGEN006`.
4. Resolve the DTO: scan **current compilation only** for types with the `[GenerateDto]` attribute whose first constructor argument (`attr.ConstructorArguments[0].Value as INamedTypeSymbol`) equals the entity. 0 → `DKCRUDGEN001`, >1 → `DKCRUDGEN002`.
5. Request name: attribute `Name` if set, else `Create{Entity}Request` / `{MethodName}{Entity}Request`.
6. Parameters → `CrudParamModel` with `PascalName` (upper first char), `TypeFullName` via `ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)`, and `AnnotationSources` = the source text of each parameter attribute whose containing namespace starts with `System.ComponentModel.DataAnnotations` (reconstruct as `[global::System.ComponentModel.DataAnnotations.Required]` etc. from the `AttributeData` — name + constructor args + named args, literal-formatted).

**Emitted request source (this task):** for entity `Product` (key `Guid`, DTO `ProductDto`, ctor `(string name, decimal price)` with `[Required] name`, method `UpdatePrice(decimal price)`):

```csharp
// <auto-generated by DKNet.SlimBus.Generators />
#nullable enable
namespace MyApi.Crud;

/// <summary>Create request generated from Product's [CrudCreate] constructor.</summary>
public sealed partial record CreateProductRequest : global::DKNet.SlimBus.Extensions.Fluents.Requests.IWitResponse<global::MyApi.Dtos.ProductDto>
{
    /// <summary>Maps to constructor parameter 'name'.</summary>
    [global::System.ComponentModel.DataAnnotations.Required]
    public required string Name { get; init; }

    /// <summary>Maps to constructor parameter 'price'.</summary>
    public required decimal Price { get; init; }
}

/// <summary>Update request generated from Product.UpdatePrice.</summary>
public sealed partial record UpdatePriceProductRequest :
    global::DKNet.SlimBus.Extensions.Fluents.Requests.IWitResponse<global::MyApi.Dtos.ProductDto>,
    global::DKNet.SlimBus.Extensions.Fluents.Requests.IWithKey<global::System.Guid>
{
    /// <summary>The target Product identifier (bound from route).</summary>
    public global::System.Guid Id { get; set; }

    /// <summary>Maps to method parameter 'price'.</summary>
    public required decimal Price { get; init; }
}
```

(Non-nullable reference/`required` rule: value types and non-nullable refs get `required ... { get; init; }`; nullable types get plain `{ get; init; }` — same convention DtoGenerator uses; read its property emission for the exact rule and mirror it.)

**csproj:** copy `src/EfCore/DKNet.EfCore.DtoGenerator/DKNet.EfCore.DtoGenerator.csproj` structure verbatim minus the DtoGenerator-specific items (no shared Compile link, no props file, no attribute contentFiles — our attributes ship in EfCore.Abstractions). Keep: netstandard2.0, `EnforceExtendedAnalyzerRules`, `IsRoslynComponent`, `IncludeBuildOutput=false`, `DevelopmentDependency`, Roslyn `VersionOverride="4.11.0"` with the same explanatory comment, analyzer `None Include` pack entry, NugetLogo + README pack entries, `InternalsVisibleTo Include="SlimBus.Generators.Tests"`.

**GeneratorTestHelper** (test project references `Microsoft.CodeAnalysis.CSharp` 4.11.0 + the generator project):

```csharp
internal static class GeneratorTestHelper
{
    public static (Compilation Output, ImmutableArray<Diagnostic> Diagnostics, GeneratorDriverRunResult Result)
        Run(string domainSource, string apiSource)
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>().ToList();
        // domain assembly compiled separately and referenced by metadata → exercises the cross-assembly path
        var domain = CSharpCompilation.Create("Domain",
            [CSharpSyntaxTree.ParseText(domainSource)], refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var api = CSharpCompilation.Create("MyApi",
            [CSharpSyntaxTree.ParseText(apiSource)], [.. refs, domain.ToMetadataReference()],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(new CrudGenerator());
        driver.RunGeneratorsAndUpdateCompilation(api, out var output, out var diags);
        return (output, diags, ((GeneratorDriver)driver).GetRunResult());
    }
}
```

The test project must reference `DKNet.EfCore.Abstractions`, `DKNet.SlimBus.Extensions`, `DKNet.EfCore.DtoGenerator` (attribute), and `DKNet.EfCore.Repos.Abstractions` so those assemblies are loaded into the AppDomain and resolvable as metadata references.

- [ ] **Step 1: Scaffold both projects + solution entries; commit scaffold** (`chore(generators): scaffold DKNet.SlimBus.Generators + tests`)
- [ ] **Step 2: Write failing tests** — `RequestEmissionTests`:
  - `Run_WithCrudCreateCtor_EmitsCreateRequestImplementingIWitResponseOfDto` (assert generated text contains `sealed partial record CreateProductRequest` and `IWitResponse<global::MyApi.ProductDto>`; assert output compilation has no errors: `output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty()`).
  - `Run_WithCrudUpdateMethod_EmitsRequestWithRouteBoundId` (contains `IWithKey<global::System.Guid>` and `public global::System.Guid Id`).
  - `Run_WithRequiredAnnotationOnParameter_CopiesAnnotationToProperty`.
  - `Run_WithNameOverride_UsesOverriddenRequestName`.
  `DiagnosticTests`: one test per DKCRUDGEN001/002/003/004/006 asserting the diagnostic id appears.
- [ ] **Step 3: Run to verify failure** — `dotnet test SlimBus.Generators.Tests` → failures (generator emits nothing yet).
- [ ] **Step 4: Implement `CrudModels`, `CrudModelBuilder`, request `Emitter`** per the shapes above. Read `DtoGenerator.cs` first for: equatable-array pattern, diagnostic descriptor pattern, property emission rules — mirror all three.
- [ ] **Step 5: Run tests to verify pass** — all green; `dotnet build DKNet.FW.sln -c Debug` zero warnings.
- [ ] **Step 6: Commit** — `feat(generators): emit CRUD request records from [CrudCreate]/[CrudUpdate] members`

---

### Task 7: Handler emission

**Files:**
- Modify: `src/SlimBus/DKNet.SlimBus.Generators/CrudGenerator.cs` / `Emitter` (add `{EntityName}CrudHandlers.g.cs` emission)
- Test: `src/SlimBus/SlimBus.Generators.Tests/HandlerEmissionTests.cs`

**Interfaces:**
- Consumes: Task 6 models; `IRepository<TEntity>` (`DKNet.EfCore.Repos.Abstractions`: `ValueTask AddAsync(TEntity, CancellationToken)`, `ValueTask<TEntity?> FindAsync(object keyValue, CancellationToken)`); `Fluents.Requests.IHandler<TRequest,TResponse>` (`Task<IResult<TResponse>> OnHandle(TRequest, CancellationToken)`); `mapper.ResultOf<TDto>` (Task 2); `NotFoundError` (Task 3). Persistence: **no SaveChanges call** — `EfAutoSavePostInterceptor` saves after the handler.
- Produces: generated internal sealed handlers, e.g.:

```csharp
/// <summary>Generated create handler for Product. Write a class implementing the same IHandler to replace it.</summary>
internal sealed class CreateProductHandler(
    global::DKNet.EfCore.Repos.Abstractions.IRepository<global::Domain.Product> repository,
    global::MapsterMapper.IMapper mapper)
    : global::DKNet.SlimBus.Extensions.Fluents.Requests.IHandler<CreateProductRequest, global::MyApi.ProductDto>
{
    /// <inheritdoc />
    public async global::System.Threading.Tasks.Task<global::FluentResults.IResult<global::MyApi.ProductDto>> OnHandle(
        CreateProductRequest request, global::System.Threading.CancellationToken cancellationToken)
    {
        var entity = new global::Domain.Product(request.Name, request.Price);
        await repository.AddAsync(entity, cancellationToken);
        return global::DKNet.SlimBus.Extensions.LazyMapper.LazyMapExtensions.ResultOf<global::MyApi.ProductDto>(mapper, entity);
    }
}

/// <summary>Generated update handler for Product.UpdatePrice.</summary>
internal sealed class UpdatePriceProductHandler(
    global::DKNet.EfCore.Repos.Abstractions.IRepository<global::Domain.Product> repository,
    global::MapsterMapper.IMapper mapper)
    : global::DKNet.SlimBus.Extensions.Fluents.Requests.IHandler<UpdatePriceProductRequest, global::MyApi.ProductDto>
{
    /// <inheritdoc />
    public async global::System.Threading.Tasks.Task<global::FluentResults.IResult<global::MyApi.ProductDto>> OnHandle(
        UpdatePriceProductRequest request, global::System.Threading.CancellationToken cancellationToken)
    {
        var entity = await repository.FindAsync(request.Id, cancellationToken);
        if (entity is null)
            return global::FluentResults.Result.Fail<global::MyApi.ProductDto>(
                new global::DKNet.SlimBus.Extensions.NotFoundError($"Product '{request.Id}' was not found."));
        entity.UpdatePrice(request.Price);
        return global::DKNet.SlimBus.Extensions.LazyMapper.LazyMapExtensions.ResultOf<global::MyApi.ProductDto>(mapper, entity);
    }
}
```

- Hand-written override: before emitting a handler, scan the **current compilation** for any type implementing `Fluents.Requests.IHandler<TThatRequest, TDto>`; if found, skip that handler and report `DKCRUDGEN005` (Info) at the hand-written type's location.

- [ ] **Step 1: Write failing tests** — `HandlerEmissionTests`:
  - `Run_WithCrudCreate_EmitsHandlerCallingCtorAndAddAsync` (generated text contains `new global::Domain.Product(` and `AddAsync`; output compilation error-free).
  - `Run_WithCrudUpdate_EmitsHandlerWithFindAsyncAndNotFoundError`.
  - `Run_WithHandWrittenHandlerPresent_SkipsGeneratedHandlerAndReportsInfo` (api source includes a manual `IHandler<CreateProductRequest, ProductDto>`; assert no generated `class CreateProductHandler` and diagnostic `DKCRUDGEN005`).
- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement handler emission + override detection.**
- [ ] **Step 4: Run tests to verify pass** (whole `SlimBus.Generators.Tests`).
- [ ] **Step 5: Commit** — `feat(generators): emit CRUD handlers with LazyMapper DTO responses and hand-written override detection`

---

### Task 8: CrudMapOptions + endpoint-registration emission

**Files:**
- Create: `src/AspNet/DKNet.AspCore.Extensions/Endpoints/CrudMapOptions.cs`
- Modify: `src/SlimBus/DKNet.SlimBus.Generators/CrudGenerator.cs` / `Emitter` (add `{EntityName}CrudEndpoints.g.cs`)
- Test: `src/AspNet/AspCore.Extensions.Tests/Endpoints/CrudMapOptionsTests.cs`
- Test: `src/SlimBus/SlimBus.Generators.Tests/EndpointEmissionTests.cs`

**Interfaces:**
- Consumes: Task 5 `MapPutById`, existing `MapGetById/MapGetList/MapDeleteById` (`FluentsEntityEndpointMapperExtensions`, type order `TEntity, TKey, TModel`) and `MapPost` (`FluentsEndpointMapperExtensions`).
- Produces:
  - `namespace DKNet.AspCore.Extensions.Endpoints;`
    `public enum CrudOp { GetById, GetList, Create, Update, Delete }`
    `public sealed class CrudMapOptions { public CrudMapOptions Exclude(params CrudOp[] operations); public bool IsExcluded(CrudOp operation); }` (backed by a `HashSet<CrudOp>`; XML docs).
  - Generated extension per entity:

```csharp
/// <summary>Registers the generated CRUD endpoints for Product.</summary>
public static class ProductCrudEndpointExtensions
{
    /// <summary>Maps GET {id}, GET /, POST /, PUT {id} (per update request) and DELETE {id} for Product.</summary>
    public static global::Microsoft.AspNetCore.Routing.RouteGroupBuilder MapProductCrud(
        this global::Microsoft.AspNetCore.Routing.RouteGroupBuilder group,
        global::System.Action<global::DKNet.AspCore.Extensions.Endpoints.CrudMapOptions>? configure = null)
    {
        var options = new global::DKNet.AspCore.Extensions.Endpoints.CrudMapOptions();
        configure?.Invoke(options);
        if (!options.IsExcluded(global::DKNet.AspCore.Extensions.Endpoints.CrudOp.GetById))
            group.MapGetById<global::Domain.Product, global::System.Guid, global::MyApi.ProductDto>();
        if (!options.IsExcluded(global::DKNet.AspCore.Extensions.Endpoints.CrudOp.GetList))
            group.MapGetList<global::Domain.Product, global::System.Guid, global::MyApi.ProductDto>();
        if (!options.IsExcluded(global::DKNet.AspCore.Extensions.Endpoints.CrudOp.Delete))
            group.MapDeleteById<global::Domain.Product, global::System.Guid>();
        if (!options.IsExcluded(global::DKNet.AspCore.Extensions.Endpoints.CrudOp.Create))
            group.MapPost<CreateProductRequest, global::MyApi.ProductDto>("/");
        if (!options.IsExcluded(global::DKNet.AspCore.Extensions.Endpoints.CrudOp.Update))
            group.MapPutById<UpdatePriceProductRequest, global::System.Guid, global::MyApi.ProductDto>("{id}");
        return group;
    }
}
```

Route rule: the **first** `[CrudUpdate]` maps to `"{id}"`; each additional update maps to `"{id}/{kebab-case method name}"` (e.g. `UpdatePrice` second → `"{id}/update-price"`). Endpoint-file emission is skipped entirely (with no diagnostic) when the compilation does not reference `DKNet.AspCore.Extensions` — generator consumers that only want requests/handlers stay clean.

- [ ] **Step 1: Write failing tests** — `CrudMapOptionsTests` (Exclude/IsExcluded round-trip, nothing excluded by default); `EndpointEmissionTests` (`Run_WithFullSlice_EmitsMapCrudExtensionComposingExistingMappers` asserting the generated text contains `MapGetById<`, `MapPost<CreateProductRequest`, `MapPutById<UpdatePriceProductRequest`; `Run_WithTwoUpdates_MapsSecondUpdateToKebabRoute`; `Run_WithoutAspCoreReference_SkipsEndpointFile`).
- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement `CrudMapOptions` (+ enum) and the endpoint emitter.** Note for the emission test: the api compilation needs `DKNet.AspCore.Extensions` + ASP.NET Core assemblies as references — extend `GeneratorTestHelper` with an optional extra-references parameter and load `Microsoft.AspNetCore.Routing` etc. via a `FrameworkReference`-loaded test project (`<FrameworkReference Include="Microsoft.AspNetCore.App"/>` in the test csproj, as `AspCore.Extensions.Tests` already does — copy its approach).
- [ ] **Step 4: Run both test projects to verify pass.**
- [ ] **Step 5: Commit** — `feat(generators): emit Map{Entity}Crud endpoint registration with CrudMapOptions exclusions`

---

### Task 9: End-to-end integration slice

**Files:**
- Create: `src/SlimBus/SlimBus.Generators.Tests.Domain/SlimBus.Generators.Tests.Domain.csproj` (net10.0 classlib; references `DKNet.EfCore.Abstractions`)
- Create: `src/SlimBus/SlimBus.Generators.Tests.Domain/Catalog/Gadget.cs`
- Create: `src/SlimBus/SlimBus.Generators.Tests.Api/SlimBus.Generators.Tests.Api.csproj` (xunit web-test project; ProjectReferences: `...Tests.Domain`, `DKNet.SlimBus.Generators` (`OutputItemType="Analyzer" ReferenceOutputAssembly="false"`), `DKNet.EfCore.DtoGenerator` (analyzer, same style), `DKNet.AspCore.Extensions`, `DKNet.EfCore.Repos`, `DKNet.SlimBus.Extensions`; packages: `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.Sqlite`, SlimMessageBus host/memory packages — mirror `SlimBus.Extensions.Tests`' bus setup and `AspCore.Extensions.Tests`' host fixture)
- Create: `src/SlimBus/SlimBus.Generators.Tests.Api/GadgetDto.cs`, `TestHost.cs`, `GadgetCrudSliceTests.cs`
- Modify: `src/DKNet.FW.sln` (add both)

**Interfaces:**
- Consumes: everything from Tasks 2–8, real DI: `AddSlimBusEfCoreInterceptor`/bus registration copied from `SlimBus.Extensions.Tests`, repo registration from `DKNet.EfCore.Repos` (`SetupRepository`-style extension — grep `AddGenericRepositories|SetupRepository` in `src/EfCore/DKNet.EfCore.Repos` and use what exists), Mapster `IMapper` registration (`services.AddSingleton<IMapper>(new Mapper())` or the repo's existing pattern — copy from a test that already registers IMapper).
- Produces: proof the whole slice works with zero hand-written request/handler/endpoint code.

Domain fixture:

```csharp
public class Gadget : Entity
{
    private Gadget() { } // EF

    [CrudCreate]
    public Gadget([Required, MaxLength(100)] string name, decimal price)
    { Name = name; Price = price; AddEvent(new GadgetCreated(Id)); }

    public string Name { get; private set; } = null!;
    public decimal Price { get; private set; }

    [CrudUpdate]
    public void UpdatePrice([Range(0, 1_000_000)] decimal price) => Price = price;
}
public sealed record GadgetCreated(Guid Id);
```

Api fixture: `[GenerateDto(typeof(Gadget))] public partial record GadgetDto;` + a minimal `DbContext` with `DbSet<Gadget>` + WebApplicationFactory-style host mapping `app.MapGroup("/gadgets").MapGadgetCrud();` over SQLite (file or shared-cache in-memory connection held open — copy the SQLite fixture pattern from `AspCore.Extensions.Tests`).

- [ ] **Step 1: Scaffold the two projects; verify the generator runs** — `dotnet build SlimBus/SlimBus.Generators.Tests.Api` succeeds and `obj/.../generated/` contains `GadgetCrudRequests.g.cs`, `GadgetCrudHandlers.g.cs`, `GadgetCrudEndpoints.g.cs` (`<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` in the csproj for inspectability).
- [ ] **Step 2: Write the slice tests** (they fail only if wiring is broken — this task is integration, the TDD loop is per-assert):

```csharp
[Fact] public async Task PostGadget_WithValidBody_Returns201AndDtoBody() // POST /gadgets {"name":"g","price":5} → 201, body has name/price, Location header present
[Fact] public async Task PostGadget_WithMissingName_Returns400()          // [Required] copied → validation 400
[Fact] public async Task PutGadgetPrice_WithExistingId_Returns200AndUpdatedDto()
[Fact] public async Task PutGadgetPrice_WithUnknownId_Returns404()        // NotFoundError path
[Fact] public async Task GetGadgetById_AfterCreate_Returns200()
[Fact] public async Task GetGadgetList_AfterCreates_ReturnsPagedResponse()
[Fact] public async Task DeleteGadgetById_WithExistingId_RemovesRow()
```

Note: the 400 test requires minimal-API DataAnnotations validation to be active on the host (`builder.Services.AddValidation()` in .NET 10 / `AddProblemDetails` per how the repo's other endpoint tests do validation — check `AspCore.Extensions.Tests` host fixture; if the repo has no established pattern, wire `MiniValidation`-free built-in `.WithParameterValidation()` equivalent that exists in net10 minimal APIs; if none exists in the repo, drop this single test to a TODO comment referencing DKCRUDGEN docs and raise it in the task report instead of inventing a new validation stack).
- [ ] **Step 3: Run** — `dotnet test SlimBus.Generators.Tests.Api` → all pass locally (SQLite, no Docker).
- [ ] **Step 4: Full solution check** — `dotnet build DKNet.FW.sln -c Debug` (zero warnings) and `dotnet format` on touched projects.
- [ ] **Step 5: Commit** — `test(generators): add end-to-end generated CRUD slice over SQLite`

---

### Task 10: Docs + remote verification

**Files:**
- Create: `docs/SlimBus/DKNet.SlimBus.Generators.md` (check how existing package docs are organized under `docs/` and match — routing/index page may need a link line)
- Modify: `src/SlimBus/DKNet.SlimBus.Generators/README.md` (package README: quickstart = the Product example from the spec §Target developer experience, required consumer references list: `DKNet.SlimBus.Extensions`, `DKNet.EfCore.Repos(.Abstractions)`, `DKNet.AspCore.Extensions` (endpoints), `MapsterMapper`; diagnostics table DKCRUDGEN001–006; override story: hand-written handler wins + `CrudMapOptions.Exclude`)

- [ ] **Step 1: Write both docs** (source of truth: the spec — keep the developer-experience code block identical to the spec's).
- [ ] **Step 2: Commit** — `docs(generators): document CRUD vertical-slice generation`
- [ ] **Step 3: Full-suite verification (local; remote only for MsSql)**

Run the full solution test suite locally — every test project runs locally EXCEPT TestContainers.MsSql-backed ones (user directive 2026-08-24). This branch touches no MsSql-backed project, so no remote run is required. If an MsSql-backed project (e.g. AspCore.Idempotency.MsSqlStore.Tests) ever becomes affected, verify just that project via `gh workflow run remote-tests.yml --ref feat/crud-generation -f project=<path>`.

```bash
dotnet test DKNet.FW.sln    # from src/; expect MsSql-backed projects to be excluded or skipped per local Docker availability
```

Expected: pass. Report any failures with output.
- [ ] **Step 4: Report** — summarize coverage impact and any deviations from the spec for the PR description (PR base: `dev`; note breaking changes: none — all additions).
