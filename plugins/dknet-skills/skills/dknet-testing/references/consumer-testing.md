# Consumer testing reference

Full depth behind `SKILL.md`'s Part A: testing your own application code that depends on DKNet NuGet packages. `Product`, `ProductPriceChanged`, and `AppDbContext` below are declared once and reused by every example in this file — a separate, independent set from the ones in `SKILL.md` (each markdown file is its own compilation).

```csharp
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Abstractions.Events;
using Microsoft.EntityFrameworkCore;

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
```

## Real databases with TestContainers

**MsSql + the ARM64 fallback.** `mcr.microsoft.com/mssql/server` ships x64-only images. DKNet's own `AspCore.Idempotency.MsSqlStore.Tests` fixture (`Fixtures/ApiFixture.cs`) picks the image off `RuntimeInformation.ProcessArchitecture` and falls back to `mcr.microsoft.com/azure-sql-edge:latest` on ARM64 — that fallback runs on Apple Silicon (via Rosetta) but fails to launch on other ARM64 hosts outright (Linux/ARM boxes included). The container's own "ready for client connections" log line can print a moment before the `sa` password is actually usable, so a fixture that opens a real connection right after `StartAsync()` retries the *login*, not the container start:

```csharp
using System.Runtime.InteropServices;
using DotNet.Testcontainers.Builders;
using Microsoft.Data.SqlClient;
using Shouldly;
using Testcontainers.MsSql;
using Xunit;

public sealed class MsSqlLoginRetryTests : IAsyncLifetime
{
    private static readonly string MsSqlImage =
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "mcr.microsoft.com/azure-sql-edge:latest"
            : "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly MsSqlContainer _container = new MsSqlBuilder(MsSqlImage)
        .WithPassword($"A{Guid.NewGuid():N}a!")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("SQL Server is now ready for client connections"))
        .WithCleanUp(true)
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task OpenAsync_RightAfterStart_EventuallySucceeds()
    {
        SqlConnection? connection = null;
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            connection = new SqlConnection(_container.GetConnectionString());
            try
            {
                await connection.OpenAsync();
                break;
            }
            catch (SqlException) when (attempt < 5)
            {
                await connection.DisposeAsync();
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        connection!.State.ToString().ShouldBe("Open");
        await connection.DisposeAsync();
    }
}
```

On a non-Apple ARM64 host this whole fixture never gets that far — exclude the project locally (`dotnet test DKNet.FW.sln --filter "FullyQualifiedName!~MsSqlStore"` for the shipped store, or your own project's equivalent) and re-verify on the remote x64 workflow (`references/dknet-repo-testing.md`).

**PostgreSQL needs no such fallback** — the image already has a native ARM64 build:

```csharp
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class PostgresAvailabilityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").WithCleanUp(true).Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public void GetConnectionString_AfterStart_ReturnsUsableConnectionString() =>
        _container.GetConnectionString().ShouldNotBeNullOrWhiteSpace();
}
```

**Seeding with Bogus.** Pin the seed on the `Faker<T>` instance itself, never the process-wide `Randomizer.Seed` (which would leak into unrelated fixtures sharing the same test process) — this is exactly the pattern DKNet's `EfCore.Specifications.Tests` fixture uses:

```csharp
using Bogus;

public static class ProductFakerDemo
{
    public static List<Product> Generate()
    {
        var faker = new Faker<Product>()
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Price, f => f.Random.Decimal(1, 1000))
            .UseSeed(20260921);

        return faker.Generate(20);
    }
}
```

`Faker<T>.Generate()` needs an accessible parameterless constructor and settable properties reachable by reflection — a domain entity with only a business constructor and private setters can't be driven this way. DKNet's own `Specifications.Tests` entities (`Product`, `Category`, `Order`) are plain settable POCOs specifically so Bogus can populate them; the `Product` above works the same way.

## SQLite in-memory vs. EF Core InMemory — the real dividing line

Both look like "not a real database," and the repo-wide rule ("never use EF Core InMemory for integration tests") is easy to over-apply. The actual line, verified against every package's own test suite:

| Provider | What it's good for | Package examples |
|---|---|---|
| `Microsoft.Data.Sqlite` (`UseSqlite`) | Anything that needs real relational `SaveChanges`/model-build mechanics — hooks, events, audit capture, query filters, ordering | `EfCore.HookTests`, `EfCore.Events.Tests`, `EfCore.DataAuthorization.Tests`, parts of `EfCore.Specifications.Tests` |
| `Microsoft.EntityFrameworkCore.InMemory` | Only when the behavior under test is identical on every provider — a `ValueConverter`, a DI registration shape, a mapping — and no SQL/filter translation is on the line | `EfCore.Encryption.Tests` (encryption happens in the converter, not SQL), `SlimBus.Extensions.Tests` (interceptor/registration logic, not SQL), `EfCore.Extensions.Tests` (data-seeding/exception-handler tests only) |
| Real container (`Testcontainers.MsSql`/`.PostgreSql`) | The behavior is genuinely database-specific: generated SQL text, a sequence, a provider-only constraint, a real `DbUpdateConcurrencyException.Entries` | `EfCore.Extensions.Tests`' `WithSqlDbTests`, `EfCore.Specifications.Tests`' keyset-ordering/dynamic-predicate-null-semantics tests, every idempotency store's own suite |

`Microsoft.EntityFrameworkCore.InMemory` does not translate global query filters, generated SQL, or sequences — a `DataAuthorization` filter or a `Specifications` dynamic predicate can pass against it for the wrong reason and fail against SQL Server. That is the actual failure mode the repo-wide rule protects against, not "any provider that isn't a real server."

## Specifications, dynamic predicates, and paging

Beyond `ToQueryString()` plus materialized rows (the core pattern, in `SKILL.md`), two more DKNet-specific test shapes recur across `EfCore.Specifications.Tests`:

- **Fail-safe vs. fail-loud dynamic predicates.** `TryBuildPredicate` returns `false`/`null` for an unresolvable or mismatched property — it never throws — while the raw dynamic-LINQ-expression overload of `DynamicAnd`/`DynamicOr` (`DynamicAnd(string expression, params object?[] values)`) throws `ArgumentException` for a blocklisted pattern (`System.IO.File.Delete(@0)`, `Activator.CreateInstance(@0)`, `Process.Start(@0)`, and similar). Test both branches of a property that might not exist at runtime — `TryBuildPredicate` for a filter built from user input, the throwing overload only for a filter string you control.
- **Keyset paging needs a declared ordering.** `repo.ToKeysetPageAsync(spec, keySelector, cursor, pageSize, ct)` is an index seek with no growing `OFFSET`, but the specification passed to it must declare an ordering (`AddOrderBy`/`AddOrderByDescending`) or the keyset comparison has nothing to seek against — assert on the returned cursor/next-page shape, not just the row count, to catch a spec that silently stopped declaring one.

## Hooks, domain events, and audit logs — the full three-way wiring

`AddHook`, `AddEventPublisher`, and `AddEfCoreAuditLogs` each register an independent hook keyed to the same `DbContext` type; all three ride the one shared interceptor. Test-double hooks/publishers use a `static` list, cleared at the start of each test — the same shape DKNet's own `EfCore.HookTests`/`EfCore.Events.Tests` use, and it avoids fetching the exact keyed-scoped instance back out of the container:

```csharp
using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.AuditLogs;
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

public sealed class RecordingAuditPublisher : IAuditLogPublisher
{
    public static readonly List<AuditLogEntry> Received = [];
    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken t = default) { Received.AddRange(logs); return Task.CompletedTask; }
}

public sealed class HooksEventsAuditTests : IAsyncLifetime
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
        services.AddEfCoreAuditLogs<AppDbContext, RecordingAuditPublisher>();

        _db = services.BuildServiceProvider().GetRequiredService<AppDbContext>();
        await _db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() { _connection.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task ChangePrice_OnSave_FiresHookEventAndAuditLog()
    {
        RecordingHook.Calls.Clear();
        RecordingEventPublisher.Received.Clear();
        RecordingAuditPublisher.Received.Clear();

        var product = new Product { Name = "Widget", Price = 100m };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        product.ChangePrice(150m);
        await _db.SaveChangesAsync();

        RecordingHook.Calls.ShouldBe(["before", "after", "before", "after"]);
        RecordingEventPublisher.Received.OfType<ProductPriceChanged>().ShouldHaveSingleItem().NewPrice.ShouldBe(150m);
        RecordingAuditPublisher.Received.ShouldContain(e => e.EntityName == nameof(Product));
    }
}
```

`AddEfCoreAuditLogs`'s default `AuditLogBehaviour.IncludeAllAuditedEntities` captures every tracked entity change, not only ones carrying `[AuditLog]` — you don't need an attribute or an `AuditedEntity` base class for the hook to fire; `AuditLogBehaviour.OnlyAttributedAuditedEntities` is the opt-in that narrows it. Domain events collect during the *before*-save pass (so `[RaisesEvent]`/`AddEvent` narrowing can still read `IsModified`) but dispatch only in the *after*-save hook, once the write commits — assert on a publisher only after `await SaveChangesAsync()` has returned, never mid-flight.

## Testing generated (and hand-mapped) HTTP endpoints

A `[CrudCreate]`/`[CrudAction]`-generated slice maps its routes through the same `DKNet.AspCore.Extensions` fluent mappers a hand-written vertical slice would call directly — `group.MapGetById<TEntity,TKey,TDto>()`, `MapGetList<...>()`, `MapPost<TRequest,TDto>("/")`, `MapPutById<...>()`, `MapDeleteById<...>()`. That means the *test* is identical either way: a `WebApplicationFactory<TEntryPoint>` over the app, an `HttpClient`, and an assertion on status code and body — never a unit test that constructs the generated request/handler/DTO types directly (see `dknet-codegen` for what those look like).

`DKNet.AspCore.Extensions`' own test fixture (`Fixtures/EndpointTestHost.cs`) builds a real `WebApplication` with `UseTestServer()`, a real in-memory `IMessageBus` (`AddSlimMessageBus` + `WithProviderMemory()`), and a real `IRepositorySpec` over `UseInMemoryDatabase`, then maps every fluent mapper once under a shared route prefix and exercises it via `app.GetTestClient()` — copy that shape once you're testing more than one or two generated endpoints, rather than spinning up a fresh `WebApplicationFactory` per test.

## Testing idempotency stores

The in-process store (`AddIdempotentKey()`, no type argument) is the right default for testing *your* idempotent endpoint's behavior — `SKILL.md`'s recipe covers it. Testing the shipped *store implementations themselves* (or proving your app's registration order is right) needs their own fixtures:

**NpgsqlStore**, via `Testcontainers.PostgreSql` — the same container pattern as above, wired to `AddIdempotencyWithNpgsqlStore(...)`:

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.NpgsqlStore;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using System.Net.Http;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class NpgsqlIdempotencyTestProgram
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();
        app.MapPost("/orders", () => Results.Ok(new { Id = Guid.NewGuid() })).RequiredIdempotentKey();
        app.Run();
    }
}

public sealed class NpgsqlIdempotencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").WithCleanUp(true).Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task PostOrders_WithNpgsqlStore_ReturnsCachedResponseOnDuplicate()
    {
        var factory = new WebApplicationFactory<NpgsqlIdempotencyTestProgram>().WithWebHostBuilder(builder =>
            builder.ConfigureServices((_, services) => services.AddIdempotencyWithNpgsqlStore(
                _container.GetConnectionString(),
                o => o.ConflictHandling = IdempotentConflictHandling.CachedResult)));
        var client = factory.CreateClient();

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/orders");
        request1.Headers.Add("X-Idempotency-Key", "order-1");
        var response1 = await client.SendAsync(request1);

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/orders");
        request2.Headers.Add("X-Idempotency-Key", "order-1");
        var response2 = await client.SendAsync(request2);

        (await response1.Content.ReadAsStringAsync()).ShouldBe(await response2.Content.ReadAsStringAsync());
    }
}
```

`IdempotencyOptions.ConflictHandling` defaults to `ConflictResponse` (a plain duplicate gets a 409 `ProblemDetails` body, not the first response); the `CachedResult` override above is what makes the two bodies equal. Assert the concurrent-duplicate case by firing several requests with the same key through `Task.WhenAll` and counting successes vs. conflicts (or, under `ConflictHandling.CachedResult` as configured above, asserting every response body is identical) — not by sending one request twice sequentially, which never exercises the reservation race.

**RedisStore** — `IdempotencyRedisStore` itself is `internal`, reached only through `InternalsVisibleTo` inside its own test project (that's how its suite mocks `IConnectionMultiplexer`/`IDatabase` with Moq to drive concurrency scenarios directly against it). Consumer code has no such grant, so assert on the DI registration shape instead of resolving the provider — resolving a real one with a connection string like `"localhost:6379"` opens an actual socket the moment `IConnectionMultiplexer` is *resolved*, not registered:

```csharp
using DKNet.AspCore.Idempotency.RedisStore;
using Shouldly;
using StackExchange.Redis;
using Xunit;

public sealed class RedisRegistrationTests
{
    [Fact]
    public void AddIdempotencyWithRedisStore_ConnectionStringQuickStart_RegistersConnectionMultiplexer()
    {
        var services = new ServiceCollection();
        services.AddIdempotencyWithRedisStore("localhost:6379");

        services.ShouldContain(sd => sd.ServiceType == typeof(IConnectionMultiplexer));
    }
}
```

For an end-to-end assertion against the Redis store's actual behavior, run a real Redis in a container and exercise your endpoint through it exactly like the NpgsqlStore example above, swapping in `AddIdempotencyWithRedisStore(container.GetConnectionString())`.

## Blob storage: Azurite, MinIO, and Local

Azurite (Azure Blob emulator) and MinIO (S3-compatible) follow the identical `IAsyncLifetime` + Testcontainers shape; only the adapter registration call and its options differ.

**Azurite**, through `AddAzureStorageAdapter(IConfiguration)` — the same shape DKNet's own `Svc.BlobStorage.Tests` fixture (`AzureStorageBlobServiceFixture`) uses:

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.AzureStorage;
using Shouldly;
using Testcontainers.Azurite;
using Xunit;

public sealed class AzuriteBlobStorageTests : IAsyncLifetime
{
    private readonly AzuriteContainer _container = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.28.0")
        .WithCommand("--skipApiVersionCheck")
        .Build();
    private IBlobService _blobs = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BlobService:AzureStorage:ConnectionString"] = _container.GetConnectionString(),
            ["BlobService:AzureStorage:ContainerName"] = "test-container"
        }).Build();

        var services = new ServiceCollection().AddLogging();
        services.AddAzureStorageAdapter(config);
        _blobs = services.BuildServiceProvider().GetRequiredService<IBlobService>();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task SaveAsync_ThenCheckExistsAsync_ReturnsTrue()
    {
        var saved = new BlobDetails.BlobData("greeting.txt", new BinaryData("hello"u8.ToArray()));
        await _blobs.SaveAsync(saved);

        (await _blobs.CheckExistsAsync(new BlobRequest("greeting.txt"))).ShouldBeTrue();
    }
}
```

**MinIO**, through `AddS3BlobService(IConfiguration)` — that method takes configuration only, so build a small in-memory one:

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.AwsS3;
using Shouldly;
using Testcontainers.Minio;
using Xunit;

public sealed class MinioBlobStorageTests : IAsyncLifetime
{
    // minio/minio was withdrawn from Docker Hub for anonymous pulls; quay.io/minio/minio is MinIO's own
    // registry and still serves this tag — pinned by digest so a future re-tag can't change what runs.
    // Same image DKNet's own Svc.BlobStorage.Tests fixture (S3BlobServiceFixture) uses.
    private readonly MinioContainer _container = new MinioBuilder(
        "quay.io/minio/minio@sha256:c5cf013c67de7854d445afed42c07810c402ce5afb441af02877f8f3dc045ec4").Build();
    private IBlobService _blobs = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BlobService:S3:ConnectionString"] = _container.GetConnectionString(),
            ["BlobService:S3:AccessKey"] = _container.GetAccessKey(),
            ["BlobService:S3:Secret"] = _container.GetSecretKey(),
            ["BlobService:S3:BucketName"] = "test-bucket",
            ["BlobService:S3:ForcePathStyle"] = "true"
        }).Build();

        var services = new ServiceCollection().AddLogging();
        services.AddS3BlobService(config);
        _blobs = services.BuildServiceProvider().GetRequiredService<IBlobService>();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task SaveAsync_ThenCheckExistsAsync_ReturnsTrue()
    {
        var saved = new BlobDetails.BlobData("greeting.txt", new BinaryData("hello"u8.ToArray()));
        await _blobs.SaveAsync(saved);

        (await _blobs.CheckExistsAsync(new BlobRequest("greeting.txt"))).ShouldBeTrue();
    }
}
```

`DKNet.Svc.BlobStorage.Local` needs no container at all — point `LocalDirectoryOptions.RootFolder` (bound from configuration key `"BlobStorage:LocalFolder:RootFolder"`) at a fresh, GUID-suffixed temp directory, call `AddLocalDirectoryBlobService(config)`, and delete the directory recursively on dispose. `LocalBlobService` throws `UnauthorizedAccessException` for any path that resolves outside `RootFolder` — worth a dedicated path-traversal test (`"../../etc/passwd"`-shaped input) alongside the happy-path round trip.

## PDF generation and the PuppeteerSharp/Chromium constraint

`PuppeteerSharp` fetches an **x64** Chromium regardless of host architecture. On x64 and Apple Silicon (via Rosetta) it downloads and launches; on any other ARM64 host the download still succeeds and only the *launch* fails (`PuppeteerSharp.ProcessException: Failed to launch browser!`, `x86_64-binfmt-P: Could not open '/lib64/ld-linux-x86-64.so.2'`). That takes out only the tests that render a real PDF — everything else in a `Svc.PdfGenerators`-consuming test project still runs.

Because the browser download/launch is expensive, DKNet's own suite shares one warmed-up `IPdfGenerator` across a tagged group of test classes via `ICollectionFixture<T>`, so Chrome pays that cost once per test run rather than once per test:

```csharp
using DKNet.Svc.PdfGenerators;
using Shouldly;
using System.IO;
using Xunit;

public sealed class PdfChromeFixture : IAsyncLifetime
{
    public IPdfGenerator Generator { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Generator = new ServiceCollection().AddPdfGenerator().BuildServiceProvider().GetRequiredService<IPdfGenerator>();
        var warmupPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        await Generator.ConvertHtmlAsync("<html><body>warmup</body></html>", warmupPath);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition("PdfChrome")]
public sealed class PdfChromeCollection : ICollectionFixture<PdfChromeFixture>;

[Collection("PdfChrome")]
public sealed class PdfGeneratorTests(PdfChromeFixture fixture)
{
    [Fact]
    public async Task ConvertHtmlAsync_WithSimpleHtml_WritesNonEmptyPdf()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        var resultPath = await fixture.Generator.ConvertHtmlAsync("<html><body><h1>Hi</h1></body></html>", outputPath);

        var file = new FileInfo(resultPath);
        file.Exists.ShouldBeTrue();
        file.Length.ShouldBeGreaterThan(0);
    }
}
```

Not every render test needs to join the shared collection — a concurrency/race test that deliberately wants several `PdfGenerator` instances racing the process-wide Chrome download lock belongs *outside* it, asserting no `IOException` escapes rather than reusing the warmed-up instance. A generated PDF is verified by existence and non-zero length, not by parsing its content — the production `PdfPig` dependency is used by `TableOfContentsCreator` internally, not by the tests.

## Architecture guard tests with NetArchTest.Rules

Several DKNet packages pin an intentional dependency boundary with a reflection-based test rather than a comment — worth copying for your own layering rules:

```csharp
using NetArchTest.Rules;
using Shouldly;
using Xunit;

public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void DomainAssembly_ShouldNotDependOn_AspNetCore()
    {
        var result = Types.InAssembly(typeof(Product).Assembly)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }
}
```

`DKNet.Svc.Encryption`'s own `Architecture/WeakCryptoDependencyTests.cs` uses the same shape to assert the package never depends on `MD5`/`SHA1`/`DES`/`System.Random`, plus a deliberately-broken canary fixture (a test-only type that *does* depend on `MD5`) to prove the rule still fires — copy the canary pattern whenever you add a architecture guard, so a future refactor that silently breaks the rule doesn't go unnoticed.

## xUnit + Shouldly + Bogus conventions

- **Naming**: `MethodName_Scenario_ExpectedBehavior` — e.g. `DynamicAnd_WithMultipleConditions_CombinesCorrectly`, `TryBuildPredicate_UnknownProperty_ReturnsFalse`.
- **Never mock `DbContext` or `DbSet<T>`.** No DKNet package test does this, for any package, anywhere in the suite — build a real one (SQLite or a container) instead.
- **`IAsyncLifetime` over shared `IClassFixture` state** whenever a test can leave the `DbContext`/connection poisoned (a thrown exception, a failed save) — a plain `IClassFixture<T>` is fine for read-only, side-effect-free shared setup (e.g. `EfCore.Extensions.Tests`' snapshot tests).
- **`InternalsVisibleTo` reaches internals only from that package's *own* test project.** DKNet's own tests use it to reach `EfCoreAuditHook`, `IdempotencyInMemoryStore`, `HookRunnerInterceptor`, and similar — a consumer's test project has no such grant and must go through the public DI surface (`IRepositorySpec`, `IIdempotencyKeyStore`, `IBlobService`, ...) instead.
- **Moq is for infrastructure clients, never for `DbContext`.** The only source-verified use across the whole suite is mocking `IConnectionMultiplexer`/`IDatabase` for the Redis idempotency store.
