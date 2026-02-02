# Research: MS SQL Storage for Idempotency Keys - Phase 0

**Date**: January 30, 2026  
**Feature**: 001-mssql-idempotency-store  
**Status**: Complete - All clarifications resolved

---

## Overview

This document consolidates research findings for the MS SQL storage implementation for idempotency keys. Since no explicit NEEDS CLARIFICATION items were identified in the plan, research focuses on:

1. Best practices for EF Core 9 DbContext creation
2. TestContainers.MsSql integration patterns
3. Concurrent request handling strategies
4. Interface design and location decisions

---

## Research Task 1: EF Core 10 Best Practices for New DbContext Creation

### Decision: Use Sealed DbContext with Configurable Model Builder

**Chosen Approach (EF Core 10)**:
```csharp
public sealed class IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options) : DbContext(options)
{
    /// <summary>
    ///     DbSet for IdempotencyKey entities.
    /// </summary>
    public required DbSet<IdempotencyKey> IdempotencyKeys { get; init; }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyDbContext).Assembly);
    }
}
```

**EF Core 10 Enhancements**:
- **Primary Constructor**: Uses C# 13 primary constructor (`(DbContextOptions<IdempotencyDbContext> options)`)
- **Required DbSet**: Uses `required` keyword (EF Core 10) instead of `null!`
- **ApplyConfigurationsFromAssembly**: EF Core 10 convention-based configuration discovery
- **Sealed**: Prevents unintended inheritance, aligns with DKNet framework style

**Entity Configuration (Separate Class - EF Core 10 Pattern)**:
```csharp
/// <summary>
///     Entity configuration for IdempotencyKey using IEntityTypeConfiguration pattern.
/// </summary>
internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.HasKey(k => k.Id);
        
        builder.Property(k => k.Id)
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(k => k.Key)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(k => k.Route)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(k => k.HttpMethod)
            .IsRequired()
            .HasMaxLength(10)
            .HasColumnType("nvarchar(10) collate SQL_Latin1_General_CP1_CS_AS"); // Case-sensitive

        builder.Property(k => k.StatusCode).IsRequired();

        builder.Property(k => k.ResponseBody)
            .HasMaxLength(1048576)
            .HasColumnType("nvarchar(max)");

        builder.Property(k => k.ContentType)
            .HasMaxLength(256);

        builder.Property(k => k.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()")
            .HasPrecision(7)
            .ValueGeneratedOnAdd();

        builder.Property(k => k.ExpiresAt).HasPrecision(7);

        builder.Property(k => k.ProcessingCompletedAt).HasPrecision(7);

        builder.HasIndex(k => new { k.Route, k.HttpMethod, k.Key })
            .IsUnique()
            .HasDatabaseName("UX_IdempotencyKey_Composite");

        builder.HasIndex(k => k.ExpiresAt)
            .HasDatabaseName("IX_IdempotencyKeys_ExpiresAt");

        builder.HasIndex(k => new { k.Route, k.CreatedAt })
            .HasDatabaseName("IX_IdempotencyKeys_Route_CreatedAt");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_StatusCode_Valid", "[StatusCode] BETWEEN 100 AND 599");
            t.HasCheckConstraint("CK_ExpiresAt_Greater_Than_CreatedAt", "[ExpiresAt] > [CreatedAt]");
        });
    }
}
```

**Rationale**:
- **Primary Constructor**: Reduces boilerplate, standard in EF Core 10 + C# 13
- **Required DbSet**: EF Core 10 feature, eliminates need for `null!` suppression
- **IEntityTypeConfiguration**: Separates configuration from DbContext, improves testability
- **ApplyConfigurationsFromAssembly**: Auto-discovers all IEntityTypeConfiguration implementations
- **Case-sensitive collation**: EF Core 10 improves collation control, essential for HTTP method matching
- **Sealed**: Prevents unintended inheritance

**Alternatives Considered**:
1. ❌ **OnModelCreating inline**: Works but mixes concerns, hard to test
2. ❌ **FluentAPI without IEntityTypeConfiguration**: No separation of concerns
3. ✅ **IEntityTypeConfiguration with ApplyConfigurationsFromAssembly**: EF Core 10 best practice

**Reference**: [EF Core 10 New Features](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0), [Configuration with IEntityTypeConfiguration](https://learn.microsoft.com/en-us/ef/core/modeling/configuration/configuration-classes)

---

## Research Task 2: TestContainers.MsSql Integration Patterns in DKNet

### Decision: Use Existing TestContainers.MsSql Pattern from EfCore.Repos.Tests

**Current Implementation in DKNet**:

The project already uses TestContainers.MsSql successfully in:
- `EfCore.Repos.Tests/Fixtures/RepositoryTestBase.cs`
- `EfCore.AuditLogs.Tests/Fixtures/AuditLogsTestFixture.cs`
- `EfCore.Encryption.Tests/`

**Chosen Fixture Pattern**:

```csharp
[CollectionDefinition("IdempotencyDb Collection")]
public sealed class IdempotencyDbFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithEnvironment("ACCEPT_EULA", "Y")
        .WithEnvironment("SA_PASSWORD", "DKNetTest@123")
        .Build();

    private IdempotencyDbContext? _dbContext;
    public IdempotencyDbContext DbContext => _dbContext ?? throw new InvalidOperationException("Not initialized");

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var options = new DbContextOptionsBuilder<IdempotencyDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        _dbContext = new IdempotencyDbContext(options, ServiceProvider.CreateScope().ServiceProvider);
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
        await _container.StopAsync();
    }
}
```

**Advantages**:
- ✅ **Real SQL Server**: Catches SQL-specific behavior (transaction isolation, locking, collations)
- ✅ **Automatic cleanup**: Containers are destroyed after tests
- ✅ **Parallel test execution**: Each test class gets isolated container
- ✅ **Already in use**: Proven pattern in DKNet codebase

**Alternatives Considered**:
1. ❌ **InMemoryDatabase**: Violates DKNet Constitution Principle II (Test-First with Real Databases)
2. ❌ **SQLite in-memory**: Still not real SQL Server, misses MSSQL-specific features
3. ✅ **TestContainers.MsSql**: Preferred approach (Constitution compliant, proven)

**Reference**: [TestContainers.Net Documentation](https://testcontainers.com/modules/mssql/)

---

## Research Task 3: Concurrent Request Handling Strategies

### Decision: Use Unique Index + Insert-or-Query Pattern with Retry Logic

**Selected Strategy**: Database-level unique constraint with EF Core optimistic retry

**Implementation Pattern**:

```csharp
public async ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(string idempotencyKey)
{
    var sanitizedKey = SanitizeKey(idempotencyKey);
    
    // First, try to query existing
    var existing = await _context.IdempotencyKeys
        .AsNoTracking()
        .FirstOrDefaultAsync(k => k.Key == sanitizedKey)
        .ConfigureAwait(false);
    
    if (existing != null && !existing.IsExpired)
    {
        return (true, ConvertToResponse(existing));
    }
    
    return (false, null);
}

public async ValueTask MarkKeyAsProcessedAsync(string idempotencyKey, CachedResponse cachedResponse)
{
    var sanitizedKey = SanitizeKey(idempotencyKey);
    var entity = new IdempotencyKey
    {
        Key = sanitizedKey,
        Route = GetRoute(),
        HttpMethod = GetHttpMethod(),
        StatusCode = cachedResponse.StatusCode,
        ResponseBody = cachedResponse.Body,
        ContentType = cachedResponse.ContentType,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.Add(_options.Expiration),
        IsProcessed = true
    };

    try
    {
        _context.IdempotencyKeys.Add(entity);
        await _context.SaveChangesAsync().ConfigureAwait(false);
    }
    catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 2601)
    {
        // Unique constraint violation - another request processed same key
        _logger.LogDebug("Idempotency key already processed (expected in concurrent scenario): {Key}", sanitizedKey);
        // No action needed - caller will retry query
    }
}
```

**Concurrency Handling**:

| Scenario | Behavior | SQL-Level |
|----------|----------|-----------|
| Request 1 arrives first | Inserts successfully | No conflict |
| Request 2 arrives before #1 completes | Insert fails (unique constraint) | 2601 error |
| Caller retries query after insert failure | Finds existing entry, returns cached response | SELECT finds record |
| Request 3 arrives after expiration | Old entry is ignored, new processing occurs | Expired entry cleaned up |

**Advantages**:
- ✅ **Database-guaranteed atomicity**: No application-level race conditions
- ✅ **No locking required**: MSSQL handles unique constraint enforcement
- ✅ **Simple to understand**: Clear semantics (insert or query)
- ✅ **Error-driven flow**: Natural exception handling for concurrent case

**Alternatives Considered**:
1. ❌ **SERIALIZABLE isolation level**: Overly strict, impacts performance, not needed
2. ❌ **SELECT FOR UPDATE**: Not available in SQL Server (would use `UPDLOCK` hint)
3. ✅ **Unique index + exception handling**: Proven pattern, minimal overhead

**Reference**: [SQL Server Unique Constraints](https://learn.microsoft.com/en-us/sql/relational-databases/tables/unique-constraints-and-check-constraints), [EF Core Exception Handling](https://learn.microsoft.com/en-us/ef/core/change-tracking/change-detection)

---

## Research Task 4: Interface Design and Location Decision

### Decision: Keep IIdempotencyKeyStore in AspCore.Idempotency, Implement in Both Packages

**Current State**:
- `IIdempotencyKeyStore` defined in `AspNet/DKNet.AspCore.Idempotency/Store/IdempotencyKeyStore.cs`
- Only implementation: `IdempotencyDistributedCacheStore` (cache-based)

**Chosen Design**:

```
AspNet/DKNet.AspCore.Idempotency/
├── Store/
│   ├── IdempotencyKeyStore.cs (INTERFACE - define here)
│   └── IdempotencyDistributedCacheStore.cs (IMPLEMENTATION)

EfCore/DKNet.EfCore.Idempotency/
├── Store/
│   └── IdempotencySqlServerStore.cs (IMPLEMENTATION)
│
└── [References AspCore.Idempotency.Store for interface]
```

**Dependency Graph**:
```
EfCore.Idempotency ──depends on──> AspCore.Idempotency
                                           ↑
                                     (defines interface)
```

**Rationale**:
- ✅ **Separation of concerns**: AspCore defines contracts, EfCore implements specific persistence
- ✅ **No circular dependencies**: EfCore depends on AspCore (correct direction)
- ✅ **DKNet pattern alignment**: Similar to Repos (interface in Abstractions, implementations in Repos and others)
- ✅ **Minimal refactoring**: No existing code changes needed

**Alternative Considered**:
1. ❌ **Move interface to DKNet.EfCore.Abstractions**: Would require AspCore to depend on EfCore (wrong direction)
2. ❌ **Create DKNet.Abstractions.Idempotency**: Excessive complexity for one interface
3. ✅ **Keep in AspCore**: AspCore is foundational, natural location for interface

**Note**: This is different from Repository pattern where interfaces live in separate Abstractions package. IdempotencyKeyStore is tightly coupled to AspCore middleware, so AspCore is appropriate location.

---

## Research Task 5: Entity Field Design and Validation Rules

### Decision: Composite Key with Route + HttpMethod + IdempotencyKey

**Chosen Schema**:

```csharp
public sealed class IdempotencyKey
{
    public Guid Id { get; set; }                           // Primary key
    public string Key { get; set; } = null!;               // User-provided idempotency key
    public string Route { get; set; } = null!;             // e.g., "/api/orders"
    public string HttpMethod { get; set; } = null!;        // e.g., "POST"
    public int StatusCode { get; set; }                    // Cached HTTP status code
    public string? ResponseBody { get; set; }              // Cached response body (JSON/text)
    public string? ContentType { get; set; }               // e.g., "application/json"
    public DateTime CreatedAt { get; set; }                // UTC timestamp
    public DateTime ExpiresAt { get; set; }                // For cleanup
    public bool IsProcessed { get; set; }                  // Whether original request completed
    public DateTime? ProcessingCompletedAt { get; set; }   // Optional: when processing finished
}
```

**Validation Rules**:

| Field | Validation | Rationale |
|-------|-----------|-----------|
| Key | Max 128 chars, alphanumeric + hyphens | Prevents cache key injection, matches UUID format |
| Route | Max 256 chars, validates URL path format | Prevents duplicate detection across different routes |
| HttpMethod | Must be: GET, POST, PUT, DELETE, PATCH | Ensures accurate composite key (important!) |
| StatusCode | 100-599 range | Valid HTTP status codes |
| ResponseBody | NULL allowed, max 1MB | Prevents database bloat from large responses |
| ContentType | Max 256 chars, valid MIME type | Allows replay with correct content-type |
| CreatedAt | UTC, auto-set to UtcNow | Immutable once created |
| ExpiresAt | Must be > CreatedAt | Ensures valid expiration window |

**Rationale for Composite Key**:

The unique composite index `(Route, HttpMethod, Key)` is critical:
- **Issue**: Same idempotency key on different routes/methods should be treated separately
  - Example: `PUT /orders/{id}` with key `order-123` ≠ `POST /orders` with key `order-123`
- **Solution**: Include Route + HttpMethod in uniqueness constraint
- **Benefit**: Safely reuse idempotency keys across different endpoints

**Reference**: [RFC 7231 - Idempotent Methods](https://tools.ietf.org/html/rfc7231#section-4.2.2)

---

## Research Task 6: Error Handling Strategy for Database Failures

### Decision: Fail-Closed by Default with Configurable Fail-Open

**Configuration Option**:

```csharp
public sealed class IdempotencyMsSqlOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public TimeSpan Expiration { get; set; } = TimeSpan.FromHours(24);
    public bool FailOpen { get; set; } = false;  // Default: fail-closed (block on DB error)
    public ILogger<IdempotencySqlServerStore> Logger { get; set; } = null!;
}
```

**Error Handling Matrix**:

| Scenario | Fail-Closed (Default) | Fail-Open |
|----------|----------------------|-----------|
| DB unavailable, first request | Return 500, log error | Process request, log warning |
| DB unavailable, duplicate request | Return 500, log error | Process request (no caching) |
| Network timeout | Retry then 500 | Allow request through |
| Connection pool exhausted | 500 | Allow request through |
| Serialization error | 500, detailed log | Process request, log error |

**Rationale**:
- **Fail-Closed Default**: In production, better to block on database error than silently skip idempotency (security/correctness)
- **Fail-Open Option**: For systems where availability > idempotency (e.g., read-only operations)
- **Configurable**: Users choose their own risk tolerance

**Implementation**:

```csharp
try
{
    await _context.SaveChangesAsync();
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to mark idempotency key as processed: {Key}", idempotencyKey);
    
    if (!_options.FailOpen)
    {
        throw;  // Re-throw, middleware will return 500
    }
    
    _logger.LogWarning("Proceeding with fail-open mode despite database error");
    // Continue without storing idempotency key
}
```

**Reference**: [Azure SQL Transient Errors](https://learn.microsoft.com/en-us/azure/azure-sql/database/troubleshoot-common-errors-issues), [Circuit Breaker Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-circuit-breaker-pattern)

---

## Research Task 7: Migration Strategy and Database Schema Setup

### Decision: Use EF Core Code-First Migrations with Initial Migration Scaffold

**Migration Approach**:

```bash
# Generate initial migration
dotnet ef migrations add InitialIdempotencySchema \
    --project EfCore/DKNet.EfCore.Idempotency \
    --startup-project EfCore/EfCore.Idempotency.Tests

# Apply migration
dotnet ef database update \
    --project EfCore/DKNet.EfCore.Idempotency
```

**Generated Migration** (example):

```csharp
public partial class InitialIdempotencySchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "IdempotencyKeys",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Key = table.Column<string>(maxLength: 128, nullable: false),
                Route = table.Column<string>(maxLength: 256, nullable: false),
                HttpMethod = table.Column<string>(maxLength: 10, nullable: false),
                StatusCode = table.Column<int>(nullable: false),
                ResponseBody = table.Column<string>(maxLength: 1048576, nullable: true),
                ContentType = table.Column<string>(maxLength: 256, nullable: true),
                CreatedAt = table.Column<DateTime>(nullable: false),
                ExpiresAt = table.Column<DateTime>(nullable: false),
                IsProcessed = table.Column<bool>(nullable: false),
                ProcessingCompletedAt = table.Column<DateTime>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IdempotencyKeys", x => x.Id);
                table.UniqueConstraint("UX_IdempotencyKey_Composite",
                    x => new { x.Route, x.HttpMethod, x.Key });
            });

        migrationBuilder.CreateIndex(
            name: "IX_IdempotencyKeys_ExpiresAt",
            table: "IdempotencyKeys",
            column: "ExpiresAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "IdempotencyKeys");
    }
}
```

**Advantages**:
- ✅ **Version-controlled schema**: All changes tracked in git
- ✅ **Repeatable deployments**: Same migration runs on dev, staging, production
- ✅ **Reversible**: Down() method allows rollback if needed
- ✅ **Standard DKNet pattern**: Already used in AuditLogs, Encryption, other EfCore projects

**Reference**: [EF Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)

---

## Summary of Research Findings

All research tasks completed. Key decisions made:

| Decision | Choice | Confidence |
|----------|--------|-----------|
| DbContext pattern | Sealed, constructor-based DI | HIGH |
| Testing infrastructure | TestContainers.MsSql (existing pattern) | HIGH |
| Concurrency handling | Unique index + exception handling | HIGH |
| Interface location | Keep in AspCore.Idempotency | HIGH |
| Entity design | Composite key with Route+HttpMethod+Key | HIGH |
| Error handling | Fail-closed default, fail-open option | HIGH |
| Schema management | EF Core code-first migrations | HIGH |

**No blockers identified.** All decisions align with:
- ✅ DKNet Framework Constitution
- ✅ Existing project patterns
- ✅ Production-grade requirements
- ✅ Test-first methodology

---

**Phase 0 Status**: ✅ **COMPLETE**  
**Ready for**: Phase 1 Design & Contracts

Generated: January 30, 2026
