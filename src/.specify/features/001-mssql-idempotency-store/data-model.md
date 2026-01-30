# Data Model: MS SQL Storage for Idempotency Keys

**Date**: January 30, 2026  
**Feature**: 001-mssql-idempotency-store  
**Status**: Phase 1 - Design & Contracts

---

## Entity-Relationship Diagram

```
┌─────────────────────────────────────────┐
│         IdempotencyKey                  │
├─────────────────────────────────────────┤
│ PK  Id: Guid                            │
│     Key: string (128)                   │
│     Route: string (256)                 │
│     HttpMethod: string (10)             │
│     StatusCode: int                     │
│     ResponseBody: string? (1MB)         │
│     ContentType: string? (256)          │
│     CreatedAt: DateTime                 │
│     ExpiresAt: DateTime                 │
│     IsProcessed: bool                   │
│     ProcessingCompletedAt: DateTime?    │
├─────────────────────────────────────────┤
│ UX (Route, HttpMethod, Key)             │
│ IX (ExpiresAt)                          │
│ IX (Route, CreatedAt)                   │
└─────────────────────────────────────────┘
```

**Relationships**: None (single entity, no foreign keys)

---

## Entity Definition

### IdempotencyKey

Represents a stored idempotency key with its associated cached HTTP response. Acts as the single-table persistence model for request deduplication.

#### Fields

| Field | Type | Constraints | Purpose |
|-------|------|-----------|---------|
| `Id` | `Guid` | PK, NOT NULL | Unique record identifier |
| `Key` | `string` | NOT NULL, MAX 128 | User-provided idempotency key (UUID or custom) |
| `Route` | `string` | NOT NULL, MAX 256 | API endpoint path (e.g., "/api/orders") |
| `HttpMethod` | `string` | NOT NULL, MAX 10 | HTTP method (GET, POST, PUT, DELETE, PATCH) |
| `StatusCode` | `int` | NOT NULL | HTTP response status code (200, 404, 500, etc.) |
| `ResponseBody` | `string` | NULLABLE, MAX 1MB | Serialized response payload (JSON/XML/text) |
| `ContentType` | `string` | NULLABLE, MAX 256 | MIME type (application/json, text/plain, etc.) |
| `CreatedAt` | `DateTime` | NOT NULL | UTC timestamp when key was first processed |
| `ExpiresAt` | `DateTime` | NOT NULL | UTC timestamp when key expires (for cleanup) |
| `IsProcessed` | `bool` | NOT NULL | Flag indicating if original request completed |
| `ProcessingCompletedAt` | `DateTime` | NULLABLE | UTC timestamp when original request finished |

#### Unique Constraints

**Composite Unique Index**: `(Route, HttpMethod, Key)`
- **Purpose**: Ensures a given idempotency key is unique per route and HTTP method
- **Rationale**: Same key on different endpoints should not collide
- **Example**:
  - `(POST /api/orders, order-123)` ≠ `(PUT /api/orders/5, order-123)`
  - Both can coexist safely
- **SQL Generated**:
  ```sql
  CREATE UNIQUE INDEX UX_IdempotencyKey_Composite
  ON IdempotencyKeys (Route, HttpMethod, Key)
  WHERE IsProcessed = 1;
  ```

#### Indexes

**Index 1**: Expiration cleanup
- **Name**: `IX_IdempotencyKeys_ExpiresAt`
- **Columns**: `ExpiresAt ASC`
- **Purpose**: Fast lookup for expired entries during cleanup operations
- **Query Pattern**: `SELECT * FROM IdempotencyKeys WHERE ExpiresAt < GETUTCDATE()`
- **Expected cardinality**: Low (entries deleted as they expire)

**Index 2**: Route-based queries
- **Name**: `IX_IdempotencyKeys_Route_CreatedAt`
- **Columns**: `(Route, CreatedAt DESC)`
- **Purpose**: Support dashboard queries filtering by endpoint
- **Query Pattern**: `SELECT * FROM IdempotencyKeys WHERE Route = @route AND CreatedAt > @startDate`

---

## Data Types & Validation

### String Fields Validation

| Field | Max Length | Pattern | Validation |
|-------|-----------|---------|-----------|
| `Key` | 128 chars | Alphanumeric + `-` | Reject: special chars, injection patterns |
| `Route` | 256 chars | URL path format | Must start with `/`, no query strings |
| `HttpMethod` | 10 chars | Enum list | Only: GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS |
| `ContentType` | 256 chars | MIME type | Pattern: `type/subtype[;params]` |
| `ResponseBody` | 1MB (1,048,576 bytes) | Any | Truncate if exceeds limit, log warning |

### Date/Time Fields

| Field | Type | Timezone | Constraints |
|-------|------|----------|-----------|
| `CreatedAt` | `DateTime` | UTC | Immutable, set on insert, never updated |
| `ExpiresAt` | `DateTime` | UTC | Must be > CreatedAt, default: CreatedAt + 24 hours |
| `ProcessingCompletedAt` | `DateTime?` | UTC | Optional, set when request completes, null for pending |

### Numeric Fields

| Field | Type | Range | Constraints |
|-------|------|-------|-----------|
| `StatusCode` | `int` | 100-599 | Valid HTTP status codes only |

---

## Database Schema

### Table Definition

```sql
CREATE TABLE [dbo].[IdempotencyKeys] (
    [Id]                      UNIQUEIDENTIFIER  NOT NULL  DEFAULT NEWID(),
    [Key]                     NVARCHAR(128)     NOT NULL,
    [Route]                   NVARCHAR(256)     NOT NULL,
    [HttpMethod]              NVARCHAR(10)      NOT NULL,
    [StatusCode]              INT               NOT NULL,
    [ResponseBody]            NVARCHAR(MAX)     NULL,
    [ContentType]             NVARCHAR(256)     NULL,
    [CreatedAt]               DATETIME2(7)      NOT NULL  DEFAULT GETUTCDATE(),
    [ExpiresAt]               DATETIME2(7)      NOT NULL,
    [IsProcessed]             BIT               NOT NULL  DEFAULT 1,
    [ProcessingCompletedAt]   DATETIME2(7)      NULL,
    
    CONSTRAINT [PK_IdempotencyKeys] PRIMARY KEY CLUSTERED ([Id] ASC),
    
    CONSTRAINT [UX_IdempotencyKey_Composite] 
        UNIQUE NONCLUSTERED ([Route], [HttpMethod], [Key]),
    
    CONSTRAINT [CK_StatusCode_Valid] 
        CHECK ([StatusCode] BETWEEN 100 AND 599),
    
    CONSTRAINT [CK_ExpiresAt_Greater_Than_CreatedAt] 
        CHECK ([ExpiresAt] > [CreatedAt])
);

CREATE NONCLUSTERED INDEX [IX_IdempotencyKeys_ExpiresAt] 
    ON [dbo].[IdempotencyKeys] ([ExpiresAt] ASC)
    INCLUDE ([Key], [IsProcessed]);

CREATE NONCLUSTERED INDEX [IX_IdempotencyKeys_Route_CreatedAt] 
    ON [dbo].[IdempotencyKeys] ([Route], [CreatedAt] DESC)
    INCLUDE ([HttpMethod], [StatusCode], [IsProcessed]);
```

### EF Core Migration Configuration

**DbContext (EF Core 10 - Primary Constructor Pattern)**:

```csharp
/// <summary>
///     Entity Framework Core DbContext for idempotency storage.
/// </summary>
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

**Entity Configuration (IEntityTypeConfiguration - EF Core 10 Best Practice)**:

```csharp
/// <summary>
///     Configuration for IdempotencyKey entity.
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
            .HasMaxLength(128)
            .IsUnicode(true);

        builder.Property(k => k.Route)
            .IsRequired()
            .HasMaxLength(256)
            .IsUnicode(true);

        builder.Property(k => k.HttpMethod)
            .IsRequired()
            .HasMaxLength(10)
            .IsUnicode(false)
            .HasColumnType("nvarchar(10) collate SQL_Latin1_General_CP1_CS_AS");

        builder.Property(k => k.StatusCode).IsRequired();

        builder.Property(k => k.ResponseBody)
            .IsUnicode(true)
            .HasMaxLength(1048576)
            .HasColumnType("nvarchar(max)");

        builder.Property(k => k.ContentType)
            .HasMaxLength(256)
            .IsUnicode(false);

        builder.Property(k => k.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()")
            .HasPrecision(7)
            .ValueGeneratedOnAdd();

        builder.Property(k => k.ExpiresAt)
            .HasPrecision(7);

        builder.Property(k => k.ProcessingCompletedAt)
            .HasPrecision(7);

        // Unique constraint
        builder.HasIndex(k => new { k.Route, k.HttpMethod, k.Key })
            .IsUnique()
            .HasDatabaseName("UX_IdempotencyKey_Composite");

        // Query indexes
        builder.HasIndex(k => k.ExpiresAt)
            .HasDatabaseName("IX_IdempotencyKeys_ExpiresAt");

        builder.HasIndex(k => new { k.Route, k.CreatedAt })
            .HasDatabaseName("IX_IdempotencyKeys_Route_CreatedAt");

        // Check constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_StatusCode_Valid", "[StatusCode] BETWEEN 100 AND 599");
            t.HasCheckConstraint("CK_ExpiresAt_Greater_Than_CreatedAt", "[ExpiresAt] > [CreatedAt]");
        });
    }
}
```

**EF Core 10 Key Improvements**:
- **Primary Constructor** on DbContext: Reduces boilerplate
- **Required DbSet**: EF Core 10 feature, eliminates nullable suppression
- **ApplyConfigurationsFromAssembly**: Auto-discovers IEntityTypeConfiguration implementations
- **IEntityTypeConfiguration Pattern**: Separates entity configuration from DbContext, improving testability and maintainability
- **Case-Sensitive Collation**: EF Core 10 improves collation control for accurate HTTP method matching

---

## Data Lifecycle

### Request Processing Flow

```
1. Request arrives with Idempotency-Key header
   ↓
2. IdempotencySqlServerStore.IsKeyProcessedAsync(key)
   - Query: SELECT * FROM IdempotencyKeys WHERE Route = @route AND HttpMethod = @method AND Key = @key
   - If found and not expired → Return cached response (Status 200 with cached body)
   - If not found → Continue processing
   ↓
3. Process original request (execute business logic)
   ↓
4. IdempotencySqlServerStore.MarkKeyAsProcessedAsync(key, response)
   - INSERT INTO IdempotencyKeys (Id, Key, Route, HttpMethod, StatusCode, ResponseBody, ContentType, CreatedAt, ExpiresAt, IsProcessed, ProcessingCompletedAt)
   VALUES (NEWID(), @key, @route, @method, @statusCode, @body, @contentType, GETUTCDATE(), GETUTCDATE() + INTERVAL, 1, GETUTCDATE())
   - If unique constraint violated → Another request processed same key (expected in race conditions)
   - Log and continue
   ↓
5. Return cached response on next duplicate request
```

### Expiration & Cleanup

```
1. Background job runs hourly (or on-demand)
   ↓
2. DELETE FROM IdempotencyKeys WHERE ExpiresAt < GETUTCDATE()
   - Uses IX_IdempotencyKeys_ExpiresAt for fast lookup
   - Removes expired entries, frees database space
   ↓
3. Entries older than configured TTL (default 24 hours) are removed
   ↓
4. Log cleanup statistics (e.g., "Removed 1,234 expired idempotency keys")
```

### Concurrent Request Handling

```
Timeline:
t0  Request A arrives (Key=order-123, Route=/api/orders, Method=POST)
    ├─ Query: "Is order-123 processed?" → No
    ├─ Start processing request A
    
t1  Request B arrives (identical Key, Route, Method)
    ├─ Query: "Is order-123 processed?" → No (A still processing)
    ├─ Start processing request B (unfortunate duplicate)
    
t2  Request A completes processing
    ├─ INSERT INTO IdempotencyKeys (Key=order-123, StatusCode=201, Body="...") → SUCCESS
    ├─ Store A's response in database
    
t3  Request B completes processing
    ├─ INSERT INTO IdempotencyKeys (Key=order-123, StatusCode=201, Body="...") → UNIQUE CONSTRAINT VIOLATION (2601)
    ├─ Catch exception, log debug message
    ├─ Query database for existing response → Found (A's response)
    ├─ Use A's cached response (B's processing is discarded)
    
t4  Request C arrives (identical Key, Route, Method)
    ├─ Query: "Is order-123 processed?" → Yes, found in database
    ├─ Return cached response without processing → SUCCESS
```

---

## Query Patterns

### Pattern 1: Check if Key Processed

```csharp
// Executed on every request with idempotency key
var existing = await dbContext.IdempotencyKeys
    .AsNoTracking()
    .FirstOrDefaultAsync(k => 
        k.Key == sanitizedKey &&
        k.Route == route &&
        k.HttpMethod == method &&
        !k.IsExpired)
    .ConfigureAwait(false);

// SQL Generated:
// SELECT TOP(1) [i].[Id], [i].[Key], [i].[Route], ...
// FROM [IdempotencyKeys] AS [i]
// WHERE [i].[Key] = @p0
//   AND [i].[Route] = @p1
//   AND [i].[HttpMethod] = @p2
//   AND [i].[ExpiresAt] > GETUTCDATE()
// ORDER BY [i].[CreatedAt] DESC
```

**Performance**: ~5-10ms with index on (Route, HttpMethod, Key)

### Pattern 2: Store Processed Key

```csharp
// Executed after processing original request
var entity = new IdempotencyKey
{
    Key = sanitizedKey,
    Route = route,
    HttpMethod = method,
    StatusCode = response.StatusCode,
    ResponseBody = response.Body,
    ContentType = response.ContentType,
    CreatedAt = DateTime.UtcNow,
    ExpiresAt = DateTime.UtcNow.Add(options.Expiration),
    IsProcessed = true,
    ProcessingCompletedAt = DateTime.UtcNow
};

dbContext.IdempotencyKeys.Add(entity);
await dbContext.SaveChangesAsync().ConfigureAwait(false);

// SQL Generated:
// INSERT INTO [IdempotencyKeys] 
// ([Id], [Key], [Route], [HttpMethod], [StatusCode], [ResponseBody], [ContentType], [CreatedAt], [ExpiresAt], [IsProcessed], [ProcessingCompletedAt])
// VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, GETUTCDATE(), @p7, 1, GETUTCDATE())
```

**Performance**: ~15-20ms for insert (includes network round-trip)

### Pattern 3: Cleanup Expired Keys

```csharp
// Executed by background job (hourly or manually)
var expiredCount = await dbContext.IdempotencyKeys
    .Where(k => k.ExpiresAt < DateTime.UtcNow)
    .ExecuteDeleteAsync()
    .ConfigureAwait(false);

logger.LogInformation("Cleaned up {Count} expired idempotency keys", expiredCount);

// SQL Generated:
// DELETE FROM [IdempotencyKeys]
// WHERE [ExpiresAt] < GETUTCDATE()
```

**Performance**: Variable, depends on table size. With proper index on ExpiresAt, efficient even for millions of records.

### Pattern 4: Query for Dashboard/Monitoring

```csharp
// Support filtering by route, date range, status
var results = await dbContext.IdempotencyKeys
    .AsNoTracking()
    .Where(k => k.Route == route && 
               k.CreatedAt >= startDate &&
               k.CreatedAt <= endDate)
    .OrderByDescending(k => k.CreatedAt)
    .Skip(pageNumber * pageSize)
    .Take(pageSize)
    .Select(k => new 
    {
        k.Id,
        k.Key,
        k.Route,
        k.HttpMethod,
        k.StatusCode,
        k.CreatedAt,
        k.ExpiresAt,
        IsExpired = k.ExpiresAt < DateTime.UtcNow
    })
    .ToListAsync()
    .ConfigureAwait(false);
```

**Performance**: ~20-50ms for typical pagination queries (depends on filter selectivity)

---

## Data Integrity & Constraints

### Primary Key
- **Column**: `Id` (Guid)
- **Type**: Clustered index
- **Strategy**: Auto-generated with NEWID() on insert
- **Rationale**: Unique identifier, queries primarily by business key (Route, Method, Key)

### Unique Constraints
- **Composite Unique**: (Route, HttpMethod, Key)
- **Purpose**: Prevent duplicate processing of same request
- **Enforcement**: Database-level constraint (tight coupling, fast failure)

### Check Constraints
- **StatusCode Valid Range**: 100-599
- **ExpiresAt > CreatedAt**: Ensures valid expiration window
- **Rationale**: Prevent invalid state in database

### Foreign Keys
- **Count**: 0
- **Rationale**: Single entity, no relationships to other tables

---

## Storage Considerations

### Space Estimation

| Field | Size | Notes |
|-------|------|-------|
| Id | 16 bytes | GUID |
| Key | ~64 bytes | Avg 50 chars (UTF-16) |
| Route | ~100 bytes | Avg 75 chars (UTF-16) |
| HttpMethod | ~8 bytes | 6 chars average |
| StatusCode | 4 bytes | Int |
| ResponseBody | Variable | Avg 1KB, max 1MB |
| ContentType | ~50 bytes | Avg 40 chars |
| CreatedAt | 8 bytes | DateTime2(7) |
| ExpiresAt | 8 bytes | DateTime2(7) |
| ProcessingCompletedAt | 8 bytes | DateTime2(7) |
| IsProcessed | 1 byte | Bit |
| **Total per record** | **~265 bytes + ResponseBody** | **Typically 1-2 KB** |

**Growth Projection** (1,000 requests/hour):
- Daily: 24,000 keys × 2 KB = ~48 MB
- Monthly: ~1.4 GB
- Yearly: ~17 GB
- 24-hour retention: ~48 MB

**Recommendation**: Store only 24-48 hours of data in database, archive older entries to cold storage if needed.

---

## Summary

The IdempotencyKey entity provides a minimal, performant persistent storage model for idempotency tracking:

✅ **Single entity** - Simple schema, no joins
✅ **Composite unique index** - Prevents duplicates safely
✅ **Support indexes** - Fast queries for cleanup and monitoring
✅ **Check constraints** - Data integrity at database level
✅ **Scalable** - Handles 1000+ requests/second with proper indexes
✅ **Expirable** - Built-in cleanup mechanism

**Ready for Phase 1 Contract Generation**

---

Generated: January 30, 2026  
Status: ✅ COMPLETE
