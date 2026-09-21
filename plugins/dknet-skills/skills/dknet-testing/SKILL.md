---
name: dknet-testing
description: Covers testing app code that consumes DKNet NuGet packages, and testing inside the DKNet repo. Consumer side - Testcontainers.MsSql/PostgreSql with the ProcessArchitecture.Arm64 -> azure-sql-edge fallback (Apple Silicon only); Microsoft.Data.Sqlite in-memory for hook/event/audit-log DbContext tests; asserting IRepositorySpec/Specification<TEntity> and PredicateBuilder.DynamicAnd/.AsExpandable() predicates via ToQueryString() plus rows; testing IHookAsync, domain events, and IAuditLogPublisher through a real DbContext; WebApplicationFactory<TEntryPoint> over a [CrudCreate]/[CrudAction] or hand-mapped endpoint; DKNet.AspCore.Idempotency store testing (in-process/MsSqlStore/NpgsqlStore/RedisStore); Testcontainers.Azurite/Minio for blob adapters; PuppeteerSharp's x64-only Chromium constraint on PdfGenerators; xUnit+Shouldly+Bogus, MethodName_Scenario_ExpectedBehavior naming; never EF Core InMemory or a mocked DbContext. Repo side - dotnet build/test, coverage targets, remote-tests.yml.
license: MIT
metadata:
  author: baoduy
  version: "0.1.0"
  packages: "all"
---

# Testing code built on DKNet, and testing inside the DKNet repo

This skill teaches two related but distinct things: (Part A) how to test *your own* application code that depends on DKNet NuGet packages, and (Part B) the commands and rules for running *DKNet's own* test suite when contributing to the framework. It owns no DKNet package; it is cross-cutting. Read `references/consumer-testing.md` for the full depth behind Part A and `references/dknet-repo-testing.md` for Part B.

## Packages

This skill owns no DKNet package. Route to `dknet-packages` for which DKNet package solves an application need, then come back here to test the code that uses it. The table is the third-party testing tooling the recipes below use — every one already appears in a DKNet `*.Tests.csproj`.

| Package | Install | What it gives you | Depends on | Reference |
|---|---|---|---|---|
| `Testcontainers.MsSql` | `dotnet add package Testcontainers.MsSql` | Real SQL Server (or `azure-sql-edge` on ARM64) in a container per run | Docker | How to: TestContainers |
| `Testcontainers.Azurite` | `dotnet add package Testcontainers.Azurite` | Azure Blob/Queue/Table emulator in a container | Docker | references/consumer-testing.md |
| `Microsoft.Data.Sqlite` | `dotnet add package Microsoft.Data.Sqlite` | A real relational engine, light enough for an in-memory `DbContext` test | none | Quick start |
| `Microsoft.AspNetCore.Mvc.Testing` | `dotnet add package Microsoft.AspNetCore.Mvc.Testing` | `WebApplicationFactory<TEntryPoint>` — an in-process HTTP test host | none | How to: HTTP, idempotency |
| `xunit` + `xunit.runner.visualstudio` | `dotnet add package xunit` | `[Fact]`/`[Theory]`, `IAsyncLifetime`, `ICollectionFixture<T>` | none | throughout |
| `Shouldly` | `dotnet add package Shouldly` | Fluent assertions (`ShouldBe`, `ShouldContain`, `ShouldThrowAsync<T>`, ...) | none | throughout |
| `Bogus` | `dotnet add package Bogus` | Seeded, fluent fake-data generation (`Faker<T>`) | none | How to: TestContainers |

The recipes' notes also reach for `Testcontainers.PostgreSql`, `Testcontainers.Minio`, `NetArchTest.Rules` (architecture guards), and `Moq` (mainly for the Redis idempotency store's `IConnectionMultiplexer`/`IDatabase`; DKNet's own suite mocks a plain `DbContext`/`DbSet<T>` in exactly one place, see Gotchas) — see `references/consumer-testing.md`.

## Quick start

The smallest complete DKNet test: an entity, a `DbContext`, a `Specification<TEntity>`, and an assertion against both `ToQueryString()` and the materialized rows, against real SQL rather than a mock. `Product`, `ProductPriceChanged`, and `AppDbContext` are declared once, here, and reused by every recipe below.

```csharp
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

public class Product : Entity
{
    public Product() : base(Guid.NewGuid()) { }

    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public void ChangePrice(decimal price)
    {
        Price = price;
        AddEvent(new ProductPriceChanged(Id, price));
    }
}

public sealed record ProductPriceChanged(Guid ProductId, decimal NewPrice);

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}

public sealed class ExpensiveProductsSpec : Specification<Product>
{
    public ExpensiveProductsSpec(decimal minPrice)
    {
        WithFilter(p => p.Price >= minPrice);
        AddOrderBy(p => p.Name);
    }
}

public sealed class ProductSpecTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private IRepositorySpec _repo = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        var services = new ServiceCollection().AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddSpecRepo<AppDbContext>();

        var provider = services.BuildServiceProvider();
        _db = provider.GetRequiredService<AppDbContext>();
        await _db.Database.EnsureCreatedAsync();
        _repo = provider.GetRequiredService<IRepositorySpec>();

        _db.Products.Add(new Product { Name = "Widget", Price = 150m });
        _db.Products.Add(new Product { Name = "Gadget", Price = 20m });
        await _db.SaveChangesAsync();
    }

    public Task DisposeAsync() { _connection.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task Query_WithExpensiveProductsSpec_FiltersAndOrders()
    {
        var query = _repo.Query(new ExpensiveProductsSpec(100m));
        query.ToQueryString().ShouldContain("WHERE");

        var product = (await query.ToListAsync()).ShouldHaveSingleItem();
        product.Name.ShouldBe("Widget");
    }
}
```

## Rules

1. Never use the `Microsoft.EntityFrameworkCore.InMemory` provider to assert real persistence, filtering, or SQL-translation behavior — it doesn't translate global query filters, generated SQL, or sequences. It's the right call only for pure DI/registration/value-converter tests with zero SQL semantics at stake, e.g. `DKNet.EfCore.Encryption`'s own suite.
2. Default to `Microsoft.Data.Sqlite` in-memory, not a container, for anything that only needs real relational `SaveChanges`/model-build mechanics (hooks, events, audit capture, query filters). Reach for `Testcontainers.MsSql`/`.PostgreSql` only when the behavior is genuinely SQL-Server- or PostgreSQL-specific.
3. Never mock `DbContext` or `DbSet<T>` to assert persistence, filtering, or SQL-translation behavior. Build a real `DbContext` — SQLite in-memory or a container — and assert against it. The one narrow exception is forcing a deterministic `DbUpdateConcurrencyException` to test exception-handler *wiring* (`RepositorySpecExceptionHandlerMockTests` in `EfCore.Specifications.Tests`), never persistence/query behavior.
4. Name tests `MethodName_Scenario_ExpectedBehavior`, e.g. `DynamicAnd_WithMultipleConditions_CombinesCorrectly`.
5. Prefer `IAsyncLifetime` over shared `IClassFixture` state whenever a test can leave the `DbContext`/connection poisoned in a way that would leak into the next test in the class.
6. Assert a `Specification`/dynamic-predicate query with `query.ToQueryString()` alongside the materialized rows — it proves the filter reached SQL, not just that it matched in memory.
7. `.AsExpandable()` is mandatory before `.Where(predicate)` for a hand-built LinqKit dynamic predicate against a raw `DbSet`; `IRepositorySpec.Query(...)` already calls it for you.
8. SQL Server only runs on x64 and Apple Silicon (via Rosetta). On any other ARM64 host, exclude a MsSql-backed test locally and re-verify on a real x64 runner — never swap in a different engine to make it pass.
9. `PuppeteerSharp` downloads an x64 Chromium unconditionally; on non-Apple ARM64 the download succeeds and the *launch* fails. That is expected, and not a reason to skip the rest of the suite.
10. Pin a `Bogus.Faker<T>`'s own seed with `.UseSeed(int)`, never the process-wide `Randomizer.Seed` — the latter leaks into unrelated fixtures sharing the same test process.
11. A `[CrudCreate]`/`[CrudAction]`-generated endpoint is tested exactly like a hand-mapped one: `WebApplicationFactory<TEntryPoint>` + `HttpClient`. Never hand-construct the generated request/handler/DTO types to "test them in isolation" — see `dknet-codegen`.
12. Never delete, `[Skip]`, or otherwise disable a DKNet test project to make a local run go green. Exclude it at the command line and re-validate on the remote x64 workflow — see "Working inside the DKNet repository" below.

## How to ...

### Test against real SQL Server with TestContainers, including the ARM64 fallback

**When**: the behavior under test is genuinely database-specific and SQLite isn't a faithful stand-in.

```csharp
using System.Runtime.InteropServices;
using DotNet.Testcontainers.Builders;
using Shouldly;
using Testcontainers.MsSql;
using Xunit;

public sealed class MsSqlAvailabilityTests : IAsyncLifetime
{
    private static readonly string MsSqlImage =
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "mcr.microsoft.com/azure-sql-edge:latest"   // Apple Silicon only — fails to launch on other ARM64 hosts
            : "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly MsSqlContainer _container = new MsSqlBuilder(MsSqlImage)
        .WithPassword($"A{Guid.NewGuid():N}a!")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("SQL Server is now ready for client connections"))
        .WithCleanUp(true)
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public void GetConnectionString_AfterStart_ReturnsUsableConnectionString() =>
        _container.GetConnectionString().ShouldNotBeNullOrWhiteSpace();
}
```

**Notes**:
- `PostgreSqlBuilder("postgres:16-alpine").WithCleanUp(true)` is the same shape for PostgreSQL, with no ARM64 special case.
- A first login right after `StartAsync()` can transiently fail even though the readiness line already printed — retry the login itself a few times rather than widening the wait strategy.
- Seed rows with `new Faker<Product>().RuleFor(p => p.Name, f => f.Commerce.ProductName()).UseSeed(20260921).Generate(5)` — the per-`Faker<T>` seed, never `Randomizer.Seed` (Rule 10).

### Assert dynamic predicates and specifications with `ToQueryString()`

**When**: a filter is built at runtime from `(propertyName, Ops, value)` triples and you need proof it reached SQL.

```csharp
using DKNet.EfCore.Specifications.Dynamics;
using LinqKit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

public sealed class DynamicPredicateTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        var services = new ServiceCollection().AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        _db = services.BuildServiceProvider().GetRequiredService<AppDbContext>();
        await _db.Database.EnsureCreatedAsync();

        _db.Products.Add(new Product { Name = "Widget", Price = 150m });
        _db.Products.Add(new Product { Name = "Gadget", Price = 20m });
        await _db.SaveChangesAsync();
    }

    public Task DisposeAsync() { _connection.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task DynamicAnd_WithPriceGreaterThan_TranslatesToSqlAndFilters()
    {
        var predicate = PredicateBuilder.New<Product>().And(p => p.Price > 0).DynamicAnd("Price", Ops.GreaterThan, 100m);
        var query = _db.Products.AsNoTracking().AsExpandable().Where(predicate);
        query.ToQueryString().ShouldContain("WHERE");

        var product = (await query.ToListAsync()).ShouldHaveSingleItem();
        product.Name.ShouldBe("Widget");
    }
}
```

**Notes**:
- `DynamicAnd`/`DynamicOr` resolve via `using LinqKit;`, not `DKNet.EfCore.Specifications.Dynamics` — the extension type deliberately declares that ambient namespace. `Ops` is the one member that does need the `Dynamics` `using`.
- An unresolvable property or an unconvertible value makes `DynamicAnd`/`DynamicOr` silently skip that clause — never add a manual null check "to be safe" around them.

### Test hooks and domain events through a real DbContext

**When**: an aggregate method calls `AddEvent(...)`, and you need proof the before/after-save hook and the event publisher actually fired, not just that the property changed in memory.

```csharp
using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.Extensions.Snapshots;
using DKNet.EfCore.Hooks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

public sealed class RecordingHook : HookAsync
{
    public static readonly List<string> Calls = [];
    public override Task BeforeSaveAsync(SnapshotContext c, CancellationToken t = default) { Calls.Add("before"); return Task.CompletedTask; }
    public override Task AfterSaveAsync(SnapshotContext c, CancellationToken t = default) { Calls.Add("after"); return Task.CompletedTask; }
}

public sealed class RecordingEventPublisher : IEventPublisher
{
    public static readonly List<object> Received = [];
    public Task PublishAsync(object eventObj, CancellationToken t = default) { Received.Add(eventObj); return Task.CompletedTask; }
    public Task PublishAsync(IEnumerable<object> events, CancellationToken t = default) { Received.AddRange(events); return Task.CompletedTask; }
}

public sealed class HooksAndEventsTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContextWithHook<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddHook<AppDbContext, RecordingHook>();
        services.AddEventPublisher<AppDbContext, RecordingEventPublisher>();

        _db = services.BuildServiceProvider().GetRequiredService<AppDbContext>();
        await _db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() { _connection.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task ChangePrice_OnSave_FiresHookAndEvent()
    {
        RecordingHook.Calls.Clear();
        RecordingEventPublisher.Received.Clear();

        var product = new Product { Name = "Widget", Price = 100m };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        product.ChangePrice(150m);
        await _db.SaveChangesAsync();

        RecordingHook.Calls.ShouldBe(["before", "after", "before", "after"]);
        RecordingEventPublisher.Received.OfType<ProductPriceChanged>().ShouldHaveSingleItem().NewPrice.ShouldBe(150m);
    }
}
```

**Notes**:
- Test-double hooks/publishers use a `static` list, cleared at the start of each test — the same shape DKNet's own `EfCore.HookTests`/`EfCore.Events.Tests` use, and it sidesteps fetching the exact keyed-scoped instance back out of the container.
- `AddEfCoreAuditLogs<TDbContext, TPublisher>()` wires in exactly the same way, alongside the two calls above — implement `IAuditLogPublisher.PublishAsync(IEnumerable<AuditLogEntry>, ct)` and assert `entry.EntityName`/`entry.Action` on what it receives. Its default behaviour captures every tracked entity, not only ones with `[AuditLog]`. Full three-way wiring: `references/consumer-testing.md`.
- `AddHook`, `AddEventPublisher`, and `AddEfCoreAuditLogs` each register an independent hook keyed to the same `DbContext` type — wiring one does not wire another.

### Test an HTTP endpoint — generated or hand-mapped — with WebApplicationFactory

**When**: a minimal-API endpoint (hand-mapped, or emitted by `[CrudCreate]`/`[CrudAction]`) needs an end-to-end HTTP assertion.

```csharp
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using System.Net;
using Xunit;

public sealed class ProductByIdSpec : Specification<Product>
{
    public ProductByIdSpec(Guid id) => WithFilter(p => p.Id == id);
}

public sealed class ProductApiProgram
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
        builder.Services.AddSpecRepo<AppDbContext>();

        var app = builder.Build();
        var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

        app.MapGet("/products/{id:guid}", async (Guid id, IRepositorySpec repo) =>
        {
            var product = await repo.Query(new ProductByIdSpec(id)).FirstOrDefaultAsync();
            return product is null ? Results.NotFound() : Results.Ok(product.Name);
        });

        app.Run();
    }
}

public sealed class ProductApiTests
{
    [Fact]
    public async Task GetProduct_WithSeededId_ReturnsOkAndName()
    {
        var factory = new WebApplicationFactory<ProductApiProgram>();
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = new Product { Name = "Widget", Price = 150m };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/products/{product.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("\"Widget\"");
    }
}
```

**Notes**:
- A `[CrudCreate]`/`[CrudAction]`-generated slice maps its routes through these same `DKNet.AspCore.Extensions` fluent mappers (`MapGetById`, `MapPost`, `MapPutById`, `MapDeleteById`) under the hood — test it by hitting the generated route with this same factory-plus-`HttpClient` shape; see `dknet-codegen` for what the attribute emits.
- `ProductApiProgram` is an explicit `public` type with a `public static void Main` — a top-level-statements `Program` is `internal` by default and a separate test project can't see it without `InternalsVisibleTo`.

### Test an idempotency store

**When**: an endpoint marked idempotent must return a cached response for a duplicate request, not re-run the handler.

```csharp
using DKNet.AspCore.Idempotency;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using System.Net.Http;
using Xunit;

public sealed class IdempotencyTestProgram
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddIdempotentKey(o => o.ConflictHandling = IdempotentConflictHandling.CachedResult);

        var app = builder.Build();
        app.MapPost("/orders", () => Results.Ok(new { Id = Guid.NewGuid() })).RequiredIdempotentKey();
        app.Run();
    }
}

public sealed class IdempotencyEndpointTests
{
    [Fact]
    public async Task PostOrders_SameIdempotencyKeyTwice_ReturnsCachedResponseOnce()
    {
        var factory = new WebApplicationFactory<IdempotencyTestProgram>();
        var client = factory.CreateClient();

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/orders");
        request1.Headers.Add("X-Idempotency-Key", "order-1");
        var response1 = await client.SendAsync(request1);
        var body1 = await response1.Content.ReadAsStringAsync();

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/orders");
        request2.Headers.Add("X-Idempotency-Key", "order-1");
        var response2 = await client.SendAsync(request2);
        var body2 = await response2.Content.ReadAsStringAsync();

        body1.ShouldBe(body2); // ConflictHandling.CachedResult replays response1 — the handler ran once
    }
}
```

**Notes**:
- `AddIdempotentKey()` (no store) is the in-process, infra-free default — right for this kind of test, wrong for production. Its second call is a complete no-op, config included; the first caller always wins.
- `IdempotencyOptions.ConflictHandling` defaults to `ConflictResponse` — a duplicate key gets a 409 `ProblemDetails` body by default, **not** the first response replayed. The recipe above opts into `IdempotentConflictHandling.CachedResult` explicitly so `body1.ShouldBe(body2)` holds; drop that line and the second call's body is the 409 problem text instead.
- Testing `AddIdempotencyWithMsSqlStore`/`AddIdempotencyWithNpgsqlStore` swaps in the TestContainers pattern above; testing `AddIdempotencyWithRedisStore` mocks `IConnectionMultiplexer`/`IDatabase` with Moq instead — see `references/consumer-testing.md`.

## Working inside the DKNet repository

Testing the framework's own `*.Tests` projects uses the conventions above plus repo-specific commands and a hard rule about what never gets disabled locally.

```bash
cd src
dotnet restore DKNet.FW.sln
dotnet build   DKNet.FW.sln -c Debug                                # must produce zero warnings
dotnet test    DKNet.FW.sln --settings coverage.runsettings --collect:"XPlat Code Coverage"
dotnet test    EfCore/EfCore.Specifications.Tests                   # single project
dotnet format                                                       # before opening a PR
```

On any ARM64 host that isn't Apple Silicon, exclude the MsSql-backed project instead of switching its database engine, and re-validate on a real x64 GitHub runner before merging:

```bash
dotnet test DKNet.FW.sln --filter "FullyQualifiedName!~MsSqlStore"  # whole solution, minus MsSql
gh workflow run remote-tests.yml --ref <branch>
gh run watch <run-id> --exit-status
gh run download <run-id> -n test-results     # *.trx + build.log + test.log
```

Coverage targets (CI gate is 80% overall; the rest are aspirational, not separately enforced): Core 99%, EfCore 95%, Services 90%. Never delete, `[Skip]`, or otherwise disable a test project to make a local run go green — exclude it at the command line, say so in the PR, and re-validate above. Full command reference, the `Svc.PdfGenerators.Tests` ARM64 story, and the Pre-PR checklist: `references/dknet-repo-testing.md`.

## Runtime behaviour

- Domain events dispatch only in the after-save hook, once the write commits — a test that inspects a publisher before `await SaveChangesAsync()` returns is racing it. (`[RaisesEvent]`'s update-property narrowing is the one piece that runs during the before-save pass — it reads `EntityEntry.Property(...).IsModified`, which is meaningless once the save completes; a plain `AddEvent(...)` call has no such narrowing.)
- Hooks, event dispatch, and audit-log capture are independent registrations sharing one interceptor per `DbContext` type — wiring one does not wire another.
- `WebApplicationFactory<TEntryPoint>` builds and starts the host the moment `Services`/`CreateClient()` is first touched. Seed data through `factory.Services.CreateScope()` on the same, already-created factory you call `CreateClient()` on.
- A TestContainers container is not necessarily ready for logins the instant its readiness log line prints — a flaky first-login failure right after `StartAsync()` is an environment race; retry the login, not the assertion.

## Gotchas

- **DataAuthorization's, Hooks', and Events' own suites use SQLite, not TestContainers — that is not a violation of "never use InMemory."** The repo-wide rule targets `Microsoft.EntityFrameworkCore.InMemory`, which doesn't translate query filters at all; SQLite is a real relational engine.
- **`DKNet.EfCore.Encryption`'s own tests genuinely do use `Microsoft.EntityFrameworkCore.InMemory`.** Value conversion happens in the converter, not translated SQL, so the provider is irrelevant there — the one source-verified exception, not a precedent to copy elsewhere.
- **`RepositorySpecExceptionHandlerMockTests` (in `EfCore.Specifications.Tests`) genuinely does mock `DbContext` and `DbSet<T>` with Moq.** It exists only to force a deterministic `DbUpdateConcurrencyException` out of `SaveChangesAsync` and assert that `IEfCoreExceptionHandler` gets invoked — a handler-wiring test, not a persistence or SQL one. The one source-verified exception to Rule 3, not a precedent to copy elsewhere.
- **A top-level-statements `Program` is `internal` by default.** `WebApplicationFactory<Program>` from a separate test project can't see it without `InternalsVisibleTo` — give the test host an explicit `public` entry-point type instead.
- **`AddIdempotentKey()`'s second call is a complete no-op, including its config.** A test calling it twice with two different options and expecting the second to win silently gets the first.
- **Bogus's `Faker<T>.Generate()` needs an accessible parameterless constructor and settable properties.** A "rich" domain entity with only a business constructor and private setters can't be `Faker<T>`'d directly.
- **`IRepositorySpec` resolves scoped, keyed only to one `TDbContext` at a time.** A second `AddSpecRepo<TOtherDbContext>()` call in the same `IServiceCollection` is a no-op.

## Do not

- `DKNet.Testing`, `DKNet.TestKit`, `DKNet.EfCore.Testing` — none of these packages exist. DKNet ships zero test-helper NuGet packages.
- `new Mock<AppDbContext>()` / a mocked `DbSet<T>` to assert persistence, filtering, or SQL-translation behavior — build a real `DbContext` instead (see Rule 3 and Gotchas for the one narrow, handler-wiring-only exception).
- `services.AddDbContext<T>(o => o.UseInMemoryDatabase(...))` as a stand-in for "a real integration test" — this is exactly the provider Rule 1 forbids for SQL-behavior assertions.
- `IRepository<T>`, `IReadRepository<T>`, `IWriteRepository<T>`, `RepositoryFactory<TDbContext>` — removed; nothing left to construct or mock. Use `IRepositorySpec` via `AddSpecRepo<TDbContext>()`.
- `IRepositorySpec.DeleteRange<TEntity>(...)` — removed; the replacement is `BulkDeleteAsync<TEntity>(predicate, ct)`.
- `ISpecification<TEntity>.OrderByQueries` / `.OrderByDescendingQueries` — removed; ordering is the single declared sequence via `AddOrderBy`/`AddOrderByDescending`.
- `IAsyncEnumerable<T>.ToListAsync()` as a `DKNet.Fw.Extensions` method — removed. Use .NET 10's own `System.Linq.AsyncEnumerable.ToListAsync`.
- `IdempotencyDistributedCacheStore` — deleted from the assembly entirely. There is no `IDistributedCache`-backed idempotency store to test against.
- A DKNet-branded `WebApplicationFactory` subclass (e.g. `DKNetWebApplicationFactory<T>`) — does not exist. Use the plain `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<TEntryPoint>`.
- `app.UseIdempotency()` / an `[Idempotent]` attribute — not how the package works. The real calls are `services.AddIdempotentKey(...)` plus `.RequiredIdempotentKey()` per endpoint.

## Related skills

- `dknet-packages` — start there to route an application need to the DKNet package that solves it; come back here to test that code.
- `dknet-efcore-domain-model` — entity base classes and `UseAutoConfigModel` wiring; this skill assumes a model already exists to test.
- `dknet-efcore-specifications` — the full `Specification<TEntity>`/`IRepositorySpec`/dynamic-predicate surface; this skill only covers asserting it in a test.
- `dknet-efcore-save-pipeline` — the full `IHookAsync`/domain-event/audit-log API; this skill only covers the SQLite-backed test wiring around it.
- `dknet-efcore-data-security` — row-level ownership filtering and column encryption; both use the same SQLite/real-`DbContext` patterns taught here.
- `dknet-codegen` — what `[GenerateDto]`/`[CrudCreate]`/`[CrudAction]` actually emit; this skill only covers testing the result, never hand-writing it.
- `dknet-slimbus-cqrs` — SlimMessageBus handlers, auto-save, and events onto the bus; tested the same way a hand-mapped endpoint is here.
- `dknet-aspcore-api` — endpoint-group and `Result`/`ProblemDetails` conventions; read it before writing the `Program` a `WebApplicationFactory` test hosts.
- `dknet-idempotency` — the full `DKNet.AspCore.Idempotency` surface and its MsSql/Npgsql/Redis stores; this skill only covers how each is tested.
- `dknet-blob-storage` — the full `IBlobService` contract and its Azure/S3/Local adapters; this skill only covers the container patterns for testing them.
- `dknet-services` — `DKNet.Svc.Encryption`, `DKNet.Svc.PdfGenerators`, `DKNet.Svc.Transformation`; this skill only covers the Chromium constraint on testing PDF.
- `dknet-core-utilities` — `DKNet.Fw.Extensions`/`DKNet.RandomCreator`; useful for generating test data or tokens, otherwise unrelated to this skill.

## References

- [references/consumer-testing.md](references/consumer-testing.md) — full depth behind Part A: every pattern above plus NpgsqlStore/RedisStore idempotency testing, the MinIO adapter, `NetArchTest.Rules` architecture guards, and the fuller testing-notes texture pulled from every DKNet package's own test suite.
- [references/dknet-repo-testing.md](references/dknet-repo-testing.md) — full depth behind Part B: the exact `dotnet`/`gh` commands, the MsSql/ARM64 rule, `remote-tests.yml`, and the Pre-PR checklist.
- Testing strategy: https://github.com/baoduy/DKNet/blob/dev/docs/Testing-Strategy.md
- Full docs site: https://baoduy.github.io/DKNet/
