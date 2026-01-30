# Implementation Plan: MS SQL Storage for Idempotency Keys

**Branch**: `001-mssql-idempotency-store` | **Date**: January 30, 2026 | **Spec**: [/specs/001-mssql-idempotency-store/spec.md]
**Input**: Feature specification from `/specs/001-mssql-idempotency-store/spec.md`

## Summary

The DKNet.AspCore.Idempotency framework currently provides distributed cache-based storage for idempotency keys via `IDistributedCache`. This feature adds persistent MS SQL Server database storage using Entity Framework Core. The implementation provides an alternative storage backend (`IdempotencySqlServerStore`) that stores idempotency keys and their cached HTTP responses in a relational database, enabling reliable cross-instance idempotency in distributed systems.

**Primary Technical Approach**: 
- Create `EfCore.Idempotency` library with `IdempotencyKey` entity and `IdempotencyDbContext`
- Implement `IdempotencySqlServerStore` implementing `IIdempotencyKeyStore` interface
- Use EF Core for persistence with migrations support
- Add `AddIdempotencyMsSqlStore()` extension method for DI registration
- Support concurrent request handling with unique index on (Route, HttpMethod, Key)
- Include comprehensive test suite using TestContainers.MsSql

## Technical Context

**Language/Version**: C# 13 / .NET 10.0  
**Primary Dependencies**: 
- Microsoft.EntityFrameworkCore (10.0.2+)
- Microsoft.EntityFrameworkCore.SqlServer (10.0.2+)
- DKNet.AspCore.Idempotency (existing package)

**Storage**: MS SQL Server 2019+ via EF Core  
**Testing**: xUnit, Shouldly, TestContainers.MsSql  
**Target Platform**: ASP.NET Core 9+  
**Project Type**: .NET Library (adds EfCore.Idempotency package to EfCore solution folder)  
**Performance Goals**: <50ms latency for key retrieval (acceptable overhead vs distributed cache)  
**Constraints**: 
- Zero compiler warnings (`TreatWarningsAsErrors=true`)
- Nullable reference types enabled
- 85%+ test coverage
- Backward compatible with existing distributed cache store

**Scale/Scope**: 
- 2 new projects: `DKNet.EfCore.Idempotency` (library) + `EfCore.Idempotency.Tests` (tests)
- ~500 LOC (entity, DbContext, store implementation)
- ~1500 LOC (comprehensive test coverage)
- 6-8 integration test scenarios

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-evaluate after Phase 1 design.*

### DKNet Framework Constitution Alignment

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Zero Warnings Tolerance | ✅ **PASS** | .NET 10, C# 13, nullable types enforced. All code will compile with `TreatWarningsAsErrors=true`. |
| II. Test-First with Real Databases | ✅ **PASS** | TestContainers.MsSql already integrated in EfCore test infrastructure. All integration tests will use real SQL Server containers. |
| III. Async-Everywhere | ✅ **PASS** | All EF Core operations use async/await. `IIdempotencyKeyStore.IsKeyProcessedAsync` and `MarkKeyAsProcessedAsync` are ValueTask-based. |
| IV. Documentation & API Contracts | ✅ **PASS** | Will include XML docs on public classes/methods. File headers required. Extension methods documented with examples. |
| V. Security & Null Safety | ✅ **PASS** | Key sanitization (alphanumeric only), nullable reference types enforced, no secrets in code. |
| VI. Pattern Compliance | ✅ **PASS** | Repository pattern (IIdempotencyKeyStore), Specification pattern if needed for queries, EF Core best practices. |

**Constitution Violations**: NONE - Implementation is fully aligned with DKNet standards.

## Project Structure

### Documentation (this feature)

```text
.specify/features/001-mssql-idempotency-store/
├── spec.md              # Feature specification ✅ COMPLETE
├── plan.md              # This file (you are here)
├── research.md          # Phase 0 output (to be generated)
├── data-model.md        # Phase 1 output (to be generated)
├── quickstart.md        # Phase 1 output (to be generated)
├── contracts/           # Phase 1 output (to be generated)
│   ├── IdempotencyKey.schema.json
│   └── setup-guide.md
└── tasks.md             # Phase 2 output (NOT created by this plan)
```

### Source Code (repository root)

```text
# New Projects to Create

EfCore/
├── DKNet.EfCore.Idempotency/                    # NEW: Library
│   ├── Data/
│   │   ├── IdempotencyDbContext.cs
│   │   └── IdempotencyKey.cs
│   ├── Store/
│   │   ├── IdempotencySqlServerStore.cs
│   │   └── IIdempotencyKeyStore.cs (interface, may move from AspCore)
│   ├── Extensions/
│   │   └── IdempotencySetup.cs                  # AddIdempotencyMsSqlStore()
│   ├── DKNet.EfCore.Idempotency.csproj
│   └── [Standard DKNet file headers]
│
└── EfCore.Idempotency.Tests/                    # NEW: Test Project
    ├── Fixtures/
    │   └── IdempotencyDbFixture.cs              # TestContainers.MsSql setup
    ├── Store/
    │   ├── IdempotencySqlServerStoreTests.cs
    │   ├── ConcurrentRequestTests.cs
    │   └── ExpirationTests.cs
    ├── Migrations/
    │   └── InitialIdempotencySchema.cs          # EF Core migration
    ├── GlobalUsings.cs
    └── EfCore.Idempotency.Tests.csproj

# Modified Existing Projects

AspNet/
├── DKNet.AspCore.Idempotency/                  # UPDATED
│   ├── Store/
│   │   ├── IIdempotencyKeyStore.cs              # Move to EfCore.Idempotency? (DECISION)
│   │   ├── IdempotencyDistributedCacheStore.cs # Keep, existing cache implementation
│   │   └── IdempotencyKeyStore.cs              # Keep, interface definition
│   └── [rest unchanged]
│
└── AspCore.Idempotency.Tests/                  # UPDATED
    └── [no changes needed - new tests in EfCore tests]
```

**Structure Decision**: 
New dedicated `DKNet.EfCore.Idempotency` library in EfCore folder following the pattern established by other EfCore libraries (Repos, Specifications, AuditLogs, etc.). This maintains separation of concerns:
- AspCore library = HTTP middleware + distributed cache store
- EfCore library = Entity Framework persistence + SQL Server store

The `IIdempotencyKeyStore` interface will be shared from AspCore.Idempotency (or moved to Abstractions if creating a new abstraction layer).

## Design Decisions

### 1. Storage Backend Interface

The existing `IIdempotencyKeyStore` interface from AspCore.Idempotency will be implemented by both:
- `IdempotencyDistributedCacheStore` (existing, cache-based)
- `IdempotencySqlServerStore` (new, EF Core-based)

This allows swapping implementations via DI without changing calling code.

```csharp
public interface IIdempotencyKeyStore
{
    ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(string idempotencyKey);
    ValueTask MarkKeyAsProcessedAsync(string idempotencyKey, CachedResponse cachedResponse);
}
```

### 2. Entity Design

```csharp
public sealed class IdempotencyKey
{
    public Guid Id { get; set; }
    public string Key { get; set; } = null!;
    public string Route { get; set; } = null!;
    public string HttpMethod { get; set; } = null!;  // GET, POST, etc
    public int StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ContentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsProcessed { get; set; }
    public DateTime? ProcessingCompletedAt { get; set; }
}
```

**Rationale**:
- Composite unique index: (Route, HttpMethod, Key) prevents duplicate processing
- JSON stored as string for simplicity (EF Core 9 supports JSON columns, but string is more compatible)
- ExpiresAt for cleanup queries and soft-deletion patterns
- IsProcessed + ProcessingCompletedAt for tracking request state

### 3. DbContext Design

```csharp
public sealed class IdempotencyDbContext : DbContext
{
    public DbSet<IdempotencyKey> IdempotencyKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Index for fast lookup
        modelBuilder.Entity<IdempotencyKey>()
            .HasIndex(k => new { k.Route, k.HttpMethod, k.Key })
            .IsUnique()
            .HasDatabaseName("IX_IdempotencyKey_Composite");

        // Index for expiration cleanup
        modelBuilder.Entity<IdempotencyKey>()
            .HasIndex(k => k.ExpiresAt)
            .HasDatabaseName("IX_IdempotencyKey_ExpiresAt");
    }
}
```

### 4. Concurrency Handling

The unique composite index (Route, HttpMethod, Key) will handle race conditions:
- First request inserts the key and processes the request
- Second concurrent request attempts insert, hits unique constraint, queries existing result
- Alternative: Use `MERGE` statement for atomic check-and-insert (SQL Server specific)

### 5. Configuration & DI

```csharp
// In Startup/Program.cs
services.AddIdempotencyMsSqlStore(options =>
{
    options.ConnectionString = "Server=...";
    options.Expiration = TimeSpan.FromHours(24);
    options.FailOpen = false; // fail-closed by default (block on DB error)
});
```

## Complexity Tracking

| Aspect | Complexity | Rationale |
|--------|-----------|-----------|
| Database Schema | Low | Single entity, 3 simple indexes |
| EF Core Integration | Low | Standard DbContext, no advanced patterns needed |
| Concurrency Handling | Medium | Unique indexes sufficient for most cases, may need retry logic |
| Testing Setup | Medium | TestContainers.MsSql already in use, need fixture pattern |
| Configuration | Low | Extension method pattern established in DKNet |
| Documentation | Low | Following existing patterns from EfCore libraries |

**Overall Feature Complexity**: **MEDIUM** - Straightforward persistence layer with good testing infrastructure available.

## Phase Breakdown

### Phase 0: Research *(to be completed)*
- Resolve all NEEDS CLARIFICATION items (none currently identified)
- Research EF Core 9 best practices for new DbContext creation
- Verify TestContainers.MsSql patterns in existing codebase
- Document decision on IIdempotencyKeyStore interface location

### Phase 1: Design & Contracts *(to be completed)*
- Generate `data-model.md` with entity diagrams and relationships
- Generate `contracts/IdempotencyKey.schema.json` (OpenAPI-style schema)
- Generate `quickstart.md` with setup instructions and code examples
- Update agent context files with EF Core patterns

### Phase 2: Implementation *(separate task workflow)*
- Create `DKNet.EfCore.Idempotency` library project
- Implement `IdempotencyKey` entity and `IdempotencyDbContext`
- Implement `IdempotencySqlServerStore` class
- Create `AddIdempotencyMsSqlStore()` extension method
- Generate EF Core migrations
- Create comprehensive test suite
- Write integration tests with TestContainers.MsSql
- Documentation and examples

---

## Next Steps

1. **Proceed with Phase 0** - Run research agent for any clarifications
2. **Proceed to Phase 1** - Generate data model, schemas, and quickstart guide
3. **Execute Phase 2** - Follow tasks.md for implementation (to be generated by `/speckit.tasks`)

---

**Generated by**: GitHub Copilot Planning Agent  
**Status**: Ready for Phase 0 Research
