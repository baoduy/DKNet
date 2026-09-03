# Configuration & Setup

DKNet ships **no `AddDKNet()` and no `DKNetOptions`**. Every package registers itself, and where it needs
configuration it exposes its own options type. This page is about how those independent registrations *compose*:
which conventions they follow, what is bound from `IConfiguration` and what is not, what has to be registered
before what, and which page owns each package's own option table.

For a single package's exhaustive option list, follow the link in the last table on this page. Nothing here
duplicates those tables.

## Contents

- [The four registration conventions](#the-four-registration-conventions)
- [Where each extension method lives](#where-each-extension-method-lives)
- [What is bound from IConfiguration](#what-is-bound-from-iconfiguration)
- [Registration order that matters](#registration-order-that-matters)
- [A worked Program.cs](#a-worked-programcs)
- [Environment and secrets](#environment-and-secrets)
- [Test configuration](#test-configuration)
- [Per-package option tables](#per-package-option-tables)

---

## The four registration conventions

Every DKNet registration falls into one of four shapes. Recognising which one a package uses tells you where its
configuration comes from without reading its page.

| Convention | How you configure it | Packages |
|---|---|---|
| **Bound from a config section** | pass `IConfiguration`; the package binds its own named section | the three blob adapters — `AddAzureStorageAdapter`, `AddS3BlobService`, `AddLocalDirectoryBlobService` |
| **Configured by a delegate** | pass `Action<TOptions>` | `AddIdempotentKey` (`IdempotencyOptions`), `AddTransformerService` (`TransformOptions`), `AddContextualRequestPopulation` (`ContextualPopulationOptions`), `AddPdfGenerator`, and the `Action<AzureStorageOptions>` overload of `AddAzureStorageAdapter` |
| **Configured by a required argument** | pass the value itself | `AddAesGcmEncryption(base64Key)`, `AddRsaEncryption(privateKeyBase64)`, `AddIdempotencyWithMsSqlStore(connectionString)` and its Npgsql/Redis siblings |
| **Configured by a type you supply** | pass a type parameter DKNet resolves from DI | `AddEfCoreEncryption<TKeyProvider>`, `AddDataOwnerProvider<TDbContext, TProvider>`, `AddEventPublisher<TDbContext, TImpl>`, `AddEfCoreAuditLogs<TDbContext, TPublisher>`, `AddIdempotentKey<TStore>`, `AddBackgroundJob<TJob>` |

Three packages need no configuration at all — `AddSpecRepo<TDbContext>()`, `AddEncryptionServices()` (which
registers only `IShaHashing` and `IHmacHashing`), and the two `AddSlimBus*` calls take nothing but a type
parameter.

One extension is deliberately not on `IServiceCollection`: **`UseAutoConfigModel<TContext>()` is a
`DbContextOptionsBuilder<TContext>` extension.** It configures the model, so it belongs inside the
`AddDbContext`/`AddDbContextWithHook` callback — never in `OnModelCreating`, where the required
`DbContextOptionsBuilder` does not exist.

## Where each extension method lives

Several DKNet extensions are declared in `Microsoft.*` namespaces and need no `using` beyond what an ASP.NET Core
project already has; others need their own. This is the single most common "why does this not compile" cause.

| Method | Namespace to import |
|---|---|
| `UseAutoConfigModel<TContext>()` | `Microsoft.EntityFrameworkCore` |
| `AddEventPublisher<TDbContext, TImpl>()` | `Microsoft.Extensions.DependencyInjection` |
| `AddSlimBusEfCoreInterceptor<T>()`, `AddSlimBusEventPublisher<T>()` | `Microsoft.Extensions.DependencyInjection` |
| `AddAzureStorageAdapter(...)`, `AddLocalDirectoryBlobService(...)` | `Microsoft.Extensions.DependencyInjection` |
| `AddDbContextWithHook<T>()`, `AddHook<...>()` | `DKNet.EfCore.Hooks` |
| `AddSpecRepo<TDbContext>()` | `DKNet.EfCore.Specifications` |
| `AddDataOwnerProvider<TDbContext, TProvider>()` | `DKNet.EfCore.DataAuthorization` |
| `AddEfCoreAuditLogs<TDbContext, TPublisher>()` | `DKNet.EfCore.AuditLogs` |
| `AddIdempotentKey(...)`, `.RequiredIdempotentKey()` | `DKNet.AspCore.Idempotency` |
| `AddIdempotencyWith*Store(...)`, `AddIdempotency*Store(...)` | `DKNet.AspCore.Idempotency.MsSqlStore` / `.NpgsqlStore` / `.RedisStore` — the store's own namespace, not the base package's |
| `AddS3BlobService(...)` | `DKNet.Svc.BlobStorage.AwsS3` |
| `AddEncryptionServices()`, `AddAesGcmEncryption(...)`, `AddRsaEncryption(...)` | `DKNet.Svc.Encryption` |
| `AddEfCoreEncryption<TKeyProvider>()` | `DKNet.EfCore.Encryption` |
| `AddBackgroundJob<TJob>()`, `AddPdfGenerator(...)`, `AddTransformerService(...)` | `Microsoft.Extensions.DependencyInjection` |
| `AddContextualRequestPopulation(...)` | `DKNet.AspCore.Extensions.ModelBinding` |
| `.Response(...)` on a `Result` | `DKNet.AspCore.Extensions.Responses` |
| `UseEndpointConfigs(...)` | `DKNet.AspCore.Extensions.Endpoints` |
| `DefaultEntityTypeConfiguration<TEntity>` | `DKNet.EfCore.Extensions.Configurations` |
| `Specification<TEntity>` (base class for your specs) | `DKNet.EfCore.Specifications.Definitions` — **not** `DKNet.EfCore.Specifications`, which holds only `AddSpecRepo` |

## What is bound from IConfiguration

Only the blob adapters read a configuration section, and each one owns its section name as a `static string Name`
on its options type. Nothing else in DKNet reads `IConfiguration` on your behalf.

| Package | Options type | Section (`Options.Name`) |
|---|---|---|
| `DKNet.Svc.BlobStorage.AzureStorage` | `AzureStorageOptions` | `BlobService:AzureStorage` |
| `DKNet.Svc.BlobStorage.AwsS3` | `S3Options` | `BlobService:S3` |
| `DKNet.Svc.BlobStorage.Local` | `LocalDirectoryOptions` | `BlobStorage:LocalFolder` |

All three derive from `BlobServiceOptions`, so the validation keys are shared:

```json
{
  "BlobService": {
    "AzureStorage": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=***;AccountKey=***",
      "ContainerName": "documents",
      "IncludedExtensions": [ ".pdf", ".png" ],
      "MaxFileNameLength": 128,
      "MaxFileSizeInMb": 25
    },
    "S3": {
      "ConnectionString": "https://s3.us-east-1.amazonaws.com",
      "BucketName": "documents",
      "RegionEndpointName": "us-east-1",
      "AccessKey": "***",
      "Secret": "***"
    }
  },
  "BlobStorage": {
    "LocalFolder": {
      "RootFolder": "/var/lib/myapp/blobs",
      "MaxFileSizeInMb": 25
    }
  }
}
```

> The `Action<AzureStorageOptions>` overload of `AddAzureStorageAdapter` registers the options object directly
> rather than through `IOptions<>`. Prefer the `IConfiguration` overload shown above.

Everything else — connection strings for the idempotency stores, the AES key for `AddAesGcmEncryption`, the
idempotency header name — reaches DKNet as an argument or a delegate. Read it from your own configuration and
pass it in:

```csharp
using DKNet.AspCore.Idempotency.RedisStore;
using DKNet.Svc.Encryption;

builder.Services.AddIdempotencyWithRedisStore(
    builder.Configuration.GetConnectionString("Redis")!,
    options => options.Expiration = TimeSpan.FromHours(24));

builder.Services.AddAesGcmEncryption(builder.Configuration["Crypto:AesKey"]!);
```

## Registration order that matters

Most DKNet registrations are order-independent. These are the exceptions, and each one is a real failure mode:

- **`AddIdempotentKey`/`AddIdempotencyWith*Store` at service level, `.RequiredIdempotentKey()` per route.** The
  route call always adds the endpoint filter; the service call registers the `IIdempotencyKeyStore` and the
  `IdempotencyOptions` the filter depends on. Miss the service call and the endpoint fails at request time when
  the filter cannot be constructed — a loud failure, but only on the first request to that route, so cover it with
  a start-up test rather than relying on the DI container to catch it.
- **`AddIdempotentKey` is first-registration-wins.** It returns early if an `IIdempotencyKeyStore` is already
  registered, so a second call with different options is ignored rather than overriding the first. Register the
  store you want exactly once. The parameterless `AddIdempotentKey()` overload is `[Obsolete]`: its default
  distributed-cache store is not atomic under concurrency.
- **`AddDbContextWithHook` instead of `AddDbContext`, whenever anything hooks `SaveChanges`.** Domain events,
  audit logs, and ownership stamping all run as hooks; a plain `AddDbContext` registers no hook interceptor, so
  they never fire.
- **`UseAutoConfigModel<TContext>()` inside the `AddDbContext*` callback.** `DKNet.EfCore.DataAuthorization`
  attaches its query filter through the model builders that call collects, so a context that skips it gets no
  ownership filter.
- **`AddDataOwnerProvider` affects every `DbContext` in the process.** It registers its filter in
  `EfCoreSetup.GlobalModelBuilders`, which is **static**: every context calling `UseAutoConfigModel()` applies it,
  not just the one passed as `TDbContext`. A second context whose model contains `IOwnedBy` entities must also
  implement `IDataOwnerDbContext`, or keep those entities out of its model. See the
  [Migration Guide](Migration-Guide.md#upgrading-dknetefcoredataauthorization-idataownerdbcontext-is-now-required).
- **An `IMapper` registration is required before `AddEvent<TEvent>()` or `[RaisesEvent]` can work.** Both map the
  entity onto the event type; without a mapper the save throws `EventException`. `AddEvent(instance)` needs none.

## A worked Program.cs

One host wiring five packages. Every call below is in the namespace listed in the table above.

```csharp
using DKNet.AspCore.Idempotency;             // RequiredIdempotentKey
using DKNet.AspCore.Idempotency.RedisStore;  // AddIdempotencyWithRedisStore
using DKNet.EfCore.AuditLogs;                // AddEfCoreAuditLogs
using DKNet.EfCore.Hooks;                    // AddDbContextWithHook
using DKNet.EfCore.Specifications;           // AddSpecRepo
using Microsoft.EntityFrameworkCore;         // UseAutoConfigModel, UseSqlServer
using SlimMessageBus;                        // IMessageBus
using SlimMessageBus.Host;                   // AddSlimMessageBus, AddChildBus, AddServicesFromAssembly
using SlimMessageBus.Host.Memory;            // WithProviderMemory, AutoDeclareFrom
using SlimMessageBus.Host.Serialization.SystemTextJson;  // AddJsonSerializer

var builder = WebApplication.CreateBuilder(args);

// DbContext + the shared hook interceptor + convention-based model build.
builder.Services.AddDbContextWithHook<AppDbContext>(options => options
    .UseSqlServer(builder.Configuration.GetConnectionString("Default")!)
    .UseAutoConfigModel<AppDbContext>());

// Querying and persistence.
builder.Services.AddSpecRepo<AppDbContext>();

// Field-level change history, handed to a publisher you write.
builder.Services.AddEfCoreAuditLogs<AppDbContext, ConsoleAuditLogPublisher>();

// Domain events forwarded onto the bus, plus SaveChanges after a successful write.
builder.Services.AddSlimBusEventPublisher<AppDbContext>();
builder.Services.AddSlimBusEfCoreInterceptor<AppDbContext>();

// Plain SlimMessageBus: bus, provider, serializer, handler discovery.
builder.Services.AddSlimMessageBus(mbb => mbb
    .AddJsonSerializer()
    .AddServicesFromAssembly(typeof(Program).Assembly)   // discovers your Fluents handlers
    .AddChildBus("Memory", bus => bus
        .WithProviderMemory()
        .AutoDeclareFrom(typeof(Program).Assembly)));

// Retry protection, backed by Redis. The service call must come before the route call.
builder.Services.AddIdempotencyWithRedisStore(
    builder.Configuration.GetConnectionString("Redis")!,
    options => options.Expiration = TimeSpan.FromHours(24));

// Blob storage, the one family that binds its own config section.
builder.Services.AddAzureStorageAdapter(builder.Configuration);

var app = builder.Build();

app.MapPost("/products", (IMessageBus bus, CreateProduct command) => bus.Send(command))
   .RequiredIdempotentKey();

app.Run();
```

`AddServicesFromAssembly`, `AutoDeclareFrom`, `AddJsonSerializer` and `WithProviderMemory` are SlimMessageBus's
own API, not DKNet's — DKNet brings no transport provider, so pick one (`SlimMessageBus.Host.Memory`, an Azure
Service Bus provider, or another) yourself. Handler discovery is SlimMessageBus's job too: there is no
per-handler DKNet registration, and no validation pipeline — request validation is bring-your-own.

## Environment and secrets

DKNet adds nothing here; use the standard .NET configuration providers. The values worth keeping out of
`appsettings.json` are the blob adapter connection strings, the idempotency store connection string, and the AES
or RSA key material passed to `DKNet.Svc.Encryption` and `DKNet.EfCore.Encryption`.

- **Azure Key Vault** or **AWS Secrets Manager** for hosted environments
- **Environment variables** for containers — `BlobService__AzureStorage__ConnectionString` maps onto the section
  above
- **User secrets** (`dotnet user-secrets`) for local development

Logging is standard `Microsoft.Extensions.Logging`. DKNet's hooks and interceptors log under their own type
names, so `DKNet` as a category prefix filters them:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "DKNet": "Debug",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

## Test configuration

The DKNet test suite itself runs against a real SQL Server via TestContainers rather than the EF Core in-memory
provider, because in-memory masks provider behaviour that several packages depend on — see
[Testing Strategy](Testing-Strategy.md). Apply the same rule to your own tests of DKNet-backed persistence:
in-memory is fine for pure domain logic, but global query filters, generated SQL, and sequences need a real
provider.

Two DKNet-specific points when configuring a test host:

- **Keep `UseAutoConfigModel<TContext>()`.** Dropping it in tests silently removes global query filters, so an
  ownership-filtered query returns rows it never would in production.
- **Register a fake `IDataOwnerProvider` rather than bypassing the filter.** The filter is attached at model-build
  time and is not something a test can turn off per query.

## Per-package option tables

Each page below owns the exhaustive table for its own package. This page does not repeat them.

| Package | Its configuration surface |
|---|---|
| [DKNet.EfCore.Extensions](EfCore/DKNet.EfCore.Extensions.md) | `UseAutoConfigModel`, seeding, GUID v7 keys, `[Sequence]` |
| [DKNet.EfCore.Hooks](EfCore/DKNet.EfCore.Hooks.md) | `AddDbContextWithHook`, `AddHook`, hook disabling |
| [DKNet.EfCore.Events](EfCore/DKNet.EfCore.Events.md) | `AddEventPublisher`, `IEventPublisher`, `[RaisesEvent]` |
| [DKNet.EfCore.AuditLogs](EfCore/DKNet.EfCore.AuditLogs.md) | `AddEfCoreAuditLogs`, `[AuditLog]`, `[SensitiveData]`, `[IgnoreAuditLog]` |
| [DKNet.EfCore.DataAuthorization](EfCore/DKNet.EfCore.DataAuthorization.md) | `AddDataOwnerProvider`, `IDataOwnerDbContext`, `IOwnedBy` |
| [DKNet.EfCore.Encryption](EfCore/DKNet.EfCore.Encryption.md) | `AddEfCoreEncryption<TKeyProvider>`, `[Encrypted]` |
| [DKNet.EfCore.Specifications](EfCore/DKNet.EfCore.Specifications.md) | `AddSpecRepo`, `Specification<T>` builder methods, `Ops` |
| [DKNet.EfCore.DtoGenerator](EfCore/DKNet.EfCore.DtoGenerator.md) | `[GenerateDto]`, and the MSBuild properties in the [Global Exclusions Guide](EfCore/GLOBAL_EXCLUSIONS_GUIDE.md) |
| [DKNet.SlimBus.Extensions](Messaging/DKNet.SlimBus.Extensions.md) | `AddSlimBusEfCoreInterceptor`, `AddSlimBusEventPublisher` |
| [DKNet.AspCore.Extensions](AspNetCore/DKNet.AspCore.Extensions.md) | `AddContextualRequestPopulation`, `EndpointRegistrationOptions`, `[FromClaim]` |
| [DKNet.AspCore.Idempotency](AspNetCore/DKNet.AspCore.Idempotency.md) | `IdempotencyOptions` in full |
| [DKNet.AspCore.Tasks](AspNetCore/DKNet.AspCore.Tasks.md) | `AddBackgroundJob`, `AddBackgroundJobFrom` |
| [DKNet.Svc.BlobStorage.Abstractions](Services/DKNet.Svc.BlobStorage.Abstractions.md) | `BlobServiceOptions` validation keys shared by all adapters |
| [DKNet.Svc.Encryption](Services/DKNet.Svc.Encryption.md) | `AddEncryptionServices`, `AddAesGcmEncryption`, `AddRsaEncryption` |
| [DKNet.Svc.PdfGenerators](Services/DKNet.Svc.PdfGenerators.md) | `AddPdfGenerator` and the page/margin options |
| [DKNet.Svc.Transformation](Services/DKNet.Svc.Transformation.md) | `AddTransformerService`, `TransformOptions` |
| [Aspire.Hosting.ServiceBus](Aspire/Aspire.Hosting.ServiceBus.md) | `AddServiceBus` and the emulator resource |

---

## Related Documentation

- [Getting Started](Getting-Started.md)
- [Architecture Guide](Architecture.md)
- [API Reference](API-Reference.md)
- [Examples](Examples/README.md)
- [Troubleshooting](FAQ.md#troubleshooting)
