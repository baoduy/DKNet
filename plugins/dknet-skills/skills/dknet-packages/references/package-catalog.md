# Package catalog

All 29 DKNet packages (28 published to NuGet, 1 source-only), one row each. This is the complete
version of `SKILL.md`'s Packages table — it adds the entry point (the call or type to reach for
first) and drops the dependency graph, which `SKILL.md` already covers. Read the owning skill
before writing code against any package's API; this file only routes.

| Id | Install | Purpose | Entry point | Owning skill |
|---|---|---|---|---|
| `DKNet.Fw.Extensions` | `dotnet add package DKNet.Fw.Extensions` | Framework-agnostic reflection, type, string, enum, and DI-inspection helpers, plus fluent assembly/type scanning. | `TypeExtractors` (static helpers, no DI) | `dknet-core-utilities` |
| `DKNet.RandomCreator` | `dotnet add package DKNet.RandomCreator` | Cryptographically secure random string and character generation for passwords, tokens, and other secrets. | `StringCreator` | `dknet-core-utilities` |
| `DKNet.EfCore.Abstractions` | `dotnet add package DKNet.EfCore.Abstractions` | The persistence-agnostic vocabulary every other `DKNet.EfCore.*` package builds on: entity base classes, domain-event contracts, and the attributes that steer audit/sequence/mapping behaviour. | `Entity<TKey>`, `Entity`, `AuditedEntity<TKey>`, `AuditedEntity`, `AddEvent(...)` | `dknet-efcore-domain-model` |
| `DKNet.EfCore.Extensions` | `dotnet add package DKNet.EfCore.Extensions` | Convention-based entity configuration discovery, global query filters, data seeding, GUID v7 keys, SQL sequences. | `UseAutoConfigModel<TContext>()` | `dknet-efcore-domain-model` |
| `DKNet.EfCore.Relational.Helpers` | `dotnet add package DKNet.EfCore.Relational.Helpers` | Four `DbContext` extension methods for relational bookkeeping EF Core does not expose: table creation, connection access, table-name resolution, table-existence checks. | `DbContext` extension methods (table create/exists) | `dknet-efcore-domain-model` |
| `DKNet.EfCore.Specifications` | `dotnet add package DKNet.EfCore.Specifications` | Filter, includes, and order-by as one reusable object, executed through a single non-generic `IRepositorySpec`, plus a runtime dynamic predicate builder. | `AddSpecRepo<TDbContext>()` | `dknet-efcore-specifications` |
| `DKNet.EfCore.Hooks` | `dotnet add package DKNet.EfCore.Hooks` | A pluggable before/after-`SaveChanges` interceptor pipeline: one shared interceptor per `DbContext` type plus a pair of interfaces you implement. | `AddDbContextWithHook<TDbContext>()` | `dknet-efcore-save-pipeline` |
| `DKNet.EfCore.Events` | `dotnet add package DKNet.EfCore.Events` | Dispatches domain events raised by entities during `SaveChanges`, so a domain method never references a publisher or a bus. | `AddEventPublisher<TDbContext,TImpl>()` | `dknet-efcore-save-pipeline` |
| `DKNet.EfCore.AuditLogs` | `dotnet add package DKNet.EfCore.AuditLogs` | Captures a structured, field-level change record for every created, updated, or deleted entity and hands the batch to publishers you register. | `AddEfCoreAuditLogs<TDbContext,TPublisher>()` | `dknet-efcore-save-pipeline` |
| `DKNet.EfCore.DataAuthorization` | `dotnet add package DKNet.EfCore.DataAuthorization` | Row-level, ownership-based authorization: an automatic global query filter on reads plus `SaveChanges`-time owner stamping on writes. | `AddDataOwnerProvider<TDbContext,TProvider>()` | `dknet-efcore-data-security` |
| `DKNet.EfCore.Encryption` | `dotnet add package DKNet.EfCore.Encryption` | Transparent, column-level encryption for `string` properties, applied at the database boundary via a standard `ValueConverter`. | `AddEfCoreEncryption<TKeyServiceImplementation>()` | `dknet-efcore-data-security` |
| `DKNet.EfCore.DtoGenerator` | `dotnet add package DKNet.EfCore.DtoGenerator` | A Roslyn incremental source generator that emits DTO properties from an entity type at compile time. | `[GenerateDto]` | `dknet-codegen` |
| `DKNet.SlimBus.Extensions` | `dotnet add package DKNet.SlimBus.Extensions` | Fluent command/query/event interfaces on SlimMessageBus, automatic `SaveChanges` after a successful write, and domain events forwarded onto the bus. | `AddSlimBusEfCoreInterceptor<TDbContext>()` | `dknet-slimbus-cqrs` |
| `DKNet.SlimBus.Generators` | `dotnet add package DKNet.SlimBus.Generators` | Emits a whole CRUD vertical slice (request records, handlers, endpoint registration) from `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`-attributed entity members. | `[CrudCreate]`, `[CrudUpdate]`, `[CrudAction]` | `dknet-codegen` |
| `DKNet.AspCore.Extensions` | `dotnet add package DKNet.AspCore.Extensions` | Host-populated request members, discovered and versioned endpoint groups, verb-to-command mappers, generic list/read/delete endpoints, and `FluentResults`-to-`IResult` conversion. | `UseEndpointConfigs(...)`, `.Response(...)` | `dknet-aspcore-api` |
| `DKNet.AspCore.Tasks` | `dotnet add package DKNet.AspCore.Tasks` | Implement `IBackgroundTask`, register it, and one `BackgroundService` runs every registered task once when the host starts. | `AddBackgroundJob<TJob>()` | `dknet-aspcore-api` |
| `DKNet.AspCore.Idempotency` | `dotnet add package DKNet.AspCore.Idempotency` | An `IEndpointFilter` that recognises a client-supplied idempotency key, blocks the same operation from running twice, and replays or rejects the retry. | `AddIdempotentKey()`, `.RequiredIdempotentKey()` | `dknet-idempotency` |
| `DKNet.AspCore.Idempotency.Relational` | `dotnet add package DKNet.AspCore.Idempotency.Relational` | The shared EF Core building blocks the two relational stores derive from. Not referenced directly by application code. | Base classes only (not called directly) | `dknet-idempotency` |
| `DKNet.AspCore.Idempotency.MsSqlStore` | `dotnet add package DKNet.AspCore.Idempotency.MsSqlStore` | SQL Server-backed idempotency key store. | `AddIdempotencyWithMsSqlStore(connectionString)` | `dknet-idempotency` |
| `DKNet.AspCore.Idempotency.NpgsqlStore` | `dotnet add package DKNet.AspCore.Idempotency.NpgsqlStore` | PostgreSQL-backed idempotency key store. | `AddIdempotencyWithNpgsqlStore(connectionString)` | `dknet-idempotency` |
| `DKNet.AspCore.Idempotency.RedisStore` | `dotnet add package DKNet.AspCore.Idempotency.RedisStore` | Redis-backed idempotency key store using `SET NX` reservation and native key expiry, with no schema or migrations. | `AddIdempotencyWithRedisStore(connectionString)` | `dknet-idempotency` |
| `DKNet.Svc.BlobStorage.Abstractions` | `dotnet add package DKNet.Svc.BlobStorage.Abstractions` | Provider-agnostic `IBlobService` contract and shared model types. | `IBlobService` | `dknet-blob-storage` |
| `DKNet.Svc.BlobStorage.AwsS3` | `dotnet add package DKNet.Svc.BlobStorage.AwsS3` | AWS S3 adapter that also works against S3-compatible services such as MinIO and Cloudflare R2. | `AddS3BlobService(...)` | `dknet-blob-storage` |
| `DKNet.Svc.BlobStorage.AzureStorage` | `dotnet add package DKNet.Svc.BlobStorage.AzureStorage` | Azure Blob Storage adapter backed by `Azure.Storage.Blobs`. | `AddAzureStorageAdapter(...)` | `dknet-blob-storage` |
| `DKNet.Svc.BlobStorage.Local` | `dotnet add package DKNet.Svc.BlobStorage.Local` | Local-filesystem adapter that stores blobs under a configured root folder with path-traversal protection. | `AddLocalDirectoryBlobService(...)` | `dknet-blob-storage` |
| `DKNet.Svc.Encryption` | `dotnet add package DKNet.Svc.Encryption` | Explicitly-invoked cryptography: AES-GCM and RSA encryption, RSA signing, HMAC and SHA hashing, and Base64/Base64URL helpers. | `AddAesGcmEncryption(base64Key)`, `AddRsaEncryption(privateKeyBase64)`, `AddEncryptionServices()` | `dknet-services` |
| `DKNet.Svc.PdfGenerators` | `dotnet add package DKNet.Svc.PdfGenerators` | Converts HTML or Markdown into a PDF using headless Chromium (PuppeteerSharp) and Markdig, with page layout, header/footer, and margin control. | `AddPdfGenerator(options)` | `dknet-services` |
| `DKNet.Svc.Transformation` | `dotnet add package DKNet.Svc.Transformation` | Fills bracketed tokens in a template string from plain objects or string dictionaries by reflection. | `AddTransformerService()` | `dknet-services` |
| `Aspire.Hosting.ServiceBus` | ProjectReference (source-only — `<IsPackable>false</IsPackable>`, not on NuGet) | Adds the Azure Service Bus emulator as a resource inside an Aspire AppHost, so local work needs no shared cloud namespace. | AppHost resource-builder extension on `IDistributedApplicationBuilder` | `dknet-slimbus-cqrs` |

## Package count, verified

29 non-test projects exist under the DKNet source tree (excluding `EfCore.DtoGenerator.TestEntities`,
an unpublished internal test-support project counted in neither total). Of those 29, 28 carry no
`IsPackable=false` and are published to NuGet; exactly one, `Aspire.Hosting.ServiceBus`, sets
`<IsPackable>false</IsPackable>` and is source-only. Every package targets `net10.0` except the two
Roslyn generators (`DKNet.EfCore.DtoGenerator`, `DKNet.SlimBus.Generators`), which target
`netstandard2.0` so the compiler can load them — that does not change what a consuming app targets.

## Docs and source

- Docs hub: https://github.com/baoduy/DKNet/blob/dev/docs/README.md
- Per-area doc pages: `https://github.com/baoduy/DKNet/blob/dev/docs/<Area>/<Package>.md` (`Area` is
  one of `Core`, `EfCore`, `AspNetCore`, `Services`, `Messaging`, `Aspire`).
- Source tree: `https://github.com/baoduy/DKNet/tree/dev/src/<Area>/<Package>`.
- Full API/docs site: https://baoduy.github.io/DKNet/
- NuGet: `https://www.nuget.org/packages/<PackageId>` for any published id above.
