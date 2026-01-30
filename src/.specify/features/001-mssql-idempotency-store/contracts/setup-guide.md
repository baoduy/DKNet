# API & Integration Contracts

**Date**: January 30, 2026  
**Feature**: 001-mssql-idempotency-store  
**Status**: Phase 1 Design Complete

---

## Service Registration Contract

### AddIdempotencyMsSqlStore Extension Method

**Location**: `DKNet.EfCore.Idempotency/Extensions/ServiceCollectionExtensions.cs`

```csharp
namespace DKNet.AspCore.Idempotency;

/// <summary>
///     Extension methods for registering MS SQL-based idempotency storage.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds MS SQL Server-based idempotency key storage to the service collection.
    ///     Requires a DbContext to be registered separately.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Optional configuration action for idempotency options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    ///     <code>
    ///     builder.Services.AddDbContext&lt;IdempotencyDbContext&gt;(options =>
    ///         options.UseSqlServer(connectionString));
    ///     
    ///     builder.Services.AddIdempotencyMsSqlStore(options =>
    ///     {
    ///         options.Expiration = TimeSpan.FromHours(48);
    ///         options.FailOpen = false;
    ///     });
    ///     </code>
    /// </example>
    public static IServiceCollection AddIdempotencyMsSqlStore(
        this IServiceCollection services,
        Action<IdempotencyMsSqlOptions>? configure = null)
    {
        // Implementation
    }
}
```

**Configuration Contract**:

```csharp
namespace DKNet.AspCore.Idempotency;

/// <summary>
///     Configuration options for MS SQL Server idempotency storage.
/// </summary>
public sealed class IdempotencyMsSqlOptions
{
    /// <summary>
    ///     Gets or sets the time-to-live for idempotency keys.
    ///     Default: 24 hours.
    /// </summary>
    public TimeSpan Expiration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    ///     Gets or sets whether to continue request processing on database errors.
    ///     Default: false (fail-closed - block on error).
    ///     true = fail-open (allow request through on DB error)
    ///     false = fail-closed (block and return 500 on DB error)
    /// </summary>
    public bool FailOpen { get; set; } = false;

    /// <summary>
    ///     Gets or sets JSON serializer options for response body serialization.
    ///     Default: JsonSerializerOptions with PropertyNameCaseInsensitive = false.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = 
        new(JsonSerializerDefaults.Web);
}
```

---

## IIdempotencyKeyStore Implementation Contract

### IdempotencySqlServerStore Class

**Location**: `DKNet.EfCore.Idempotency/Store/IdempotencySqlServerStore.cs`

**Signature**:

```csharp
namespace DKNet.AspCore.Idempotency.Store;

/// <summary>
///     MS SQL Server implementation of the idempotency key store using Entity Framework Core.
/// </summary>
internal sealed class IdempotencySqlServerStore : IIdempotencyKeyStore
{
    /// <summary>
    ///     Initializes a new instance of the IdempotencySqlServerStore class.
    /// </summary>
    /// <param name="dbContext">The database context for idempotency storage.</param>
    /// <param name="options">Configuration options for idempotency behavior.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public IdempotencySqlServerStore(
        IdempotencyDbContext dbContext,
        IOptions<IdempotencyMsSqlOptions> options,
        ILogger<IdempotencySqlServerStore> logger)
    {
        // Implementation
    }

    /// <inheritdoc/>
    public async ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(
        string idempotencyKey)
    {
        // Implementation
    }

    /// <inheritdoc/>
    public async ValueTask MarkKeyAsProcessedAsync(
        string idempotencyKey,
        CachedResponse cachedResponse)
    {
        // Implementation
    }
}
```

**Contract Details**:

| Method | Input | Output | Behavior |
|--------|-------|--------|----------|
| `IsKeyProcessedAsync` | `idempotencyKey: string` | `(bool processed, CachedResponse? response)` | Query database for existing key. Return (true, response) if found and not expired, else (false, null). |
| `MarkKeyAsProcessedAsync` | `idempotencyKey: string`, `cachedResponse: CachedResponse` | `ValueTask` | Insert new idempotency key record. If unique constraint violated, log and return (no error). |

**Error Handling Contract**:

| Error Scenario | Fail-Closed (Default) | Fail-Open |
|---|---|---|
| Connection timeout | Throw `TimeoutException` → 500 response | Log warning, continue |
| Authentication failed | Throw `InvalidOperationException` → 500 response | Log error, continue |
| Unique constraint violation | Catch, log debug, return silently | Catch, log debug, return silently |
| Serialization error | Throw `JsonException` → 500 response | Log error, continue |

---

## Entity Configuration Contract

### IdempotencyKeyConfiguration Class

**Location**: `DKNet.EfCore.Idempotency/Data/Configurations/IdempotencyKeyConfiguration.cs`

**Contract**:

```csharp
namespace DKNet.EfCore.Idempotency.Data.Configurations;

/// <summary>
///     Entity configuration for IdempotencyKey using EF Core 10 pattern.
/// </summary>
internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        // All configuration applied in this method
        // See data-model.md for detailed configuration
    }
}
```

**Auto-Discovery Contract**:

The IdempotencyDbContext uses `ApplyConfigurationsFromAssembly`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    // Automatically discovers and applies all IEntityTypeConfiguration<T> implementations
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyDbContext).Assembly);
}
```

---

## DbContext Contract

### IdempotencyDbContext Class

**Location**: `DKNet.EfCore.Idempotency/Data/IdempotencyDbContext.cs`

**EF Core 10 Pattern**:

```csharp
namespace DKNet.EfCore.Idempotency.Data;

/// <summary>
///     Entity Framework Core DbContext for idempotency key storage.
/// </summary>
public sealed class IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options) 
    : DbContext(options)
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

**Features Contract**:

✅ **Primary Constructor**: Uses C# 13 primary constructor syntax  
✅ **Required DbSet**: Uses `required` keyword (EF Core 10)  
✅ **init-only setter**: DbSet can only be set during initialization  
✅ **Configuration Discovery**: Auto-discovers IEntityTypeConfiguration implementations  
✅ **Sealed**: Prevents inheritance (DKNet pattern)

---

## Migration Contract

### Generated Migration Structure

**Generated Filename**: `[Timestamp]_InitialIdempotencySchema.cs`

**Required Methods**:

```csharp
public partial class InitialIdempotencySchema : Migration
{
    /// <summary>
    ///     Applies the migration: creates IdempotencyKeys table with indexes and constraints.
    /// </summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Creates table, indexes, and constraints
        // See data-model.md for SQL generated
    }

    /// <summary>
    ///     Reverts the migration: drops IdempotencyKeys table.
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "IdempotencyKeys");
    }
}
```

**Application Contract**:

```bash
# Generate migration
dotnet ef migrations add InitialIdempotencySchema \
    --project DKNet.EfCore.Idempotency \
    --startup-project EfCore.Idempotency.Tests

# Apply migration
dotnet ef database update --project DKNet.EfCore.Idempotency
```

---

## HTTP Middleware Integration Contract

### Idempotency Filter Behavior

**Request Flow**:

```
1. HTTP Request arrives with Idempotency-Key header
   ↓
2. IdempotencyEndpointFilter intercepts
   ↓
3. Calls IIdempotencyKeyStore.IsKeyProcessedAsync(key)
   ├─ If found and not expired:
   │  └─ Return cached response (same status code + body)
   └─ If not found or expired:
      └─ Continue to endpoint
   ↓
4. Endpoint executes, returns response
   ↓
5. Calls IIdempotencyKeyStore.MarkKeyAsProcessedAsync(key, response)
   ├─ If insert succeeds:
   │  └─ Store cached response
   └─ If unique constraint violation (race condition):
      └─ Log and ignore (another request already stored it)
   ↓
6. Return response to client
```

**Database Query Patterns**:

```csharp
// Pattern 1: Check existing (on every request)
SELECT * FROM IdempotencyKeys
WHERE Route = @route 
  AND HttpMethod = @method 
  AND Key = @key 
  AND ExpiresAt > GETUTCDATE()

// Pattern 2: Store processed (after endpoint execution)
INSERT INTO IdempotencyKeys 
(Id, Key, Route, HttpMethod, StatusCode, ResponseBody, ContentType, CreatedAt, ExpiresAt, IsProcessed)
VALUES (NEWID(), @key, @route, @method, @status, @body, @type, GETUTCDATE(), @expiry, 1)

// Pattern 3: Cleanup expired (background job)
DELETE FROM IdempotencyKeys
WHERE ExpiresAt < GETUTCDATE()
```

---

## Dependency Injection Contract

### Required Service Registrations

**Minimum Configuration**:

```csharp
// Step 1: Register DbContext
builder.Services.AddDbContext<IdempotencyDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("IdempotencyDb");
    options.UseSqlServer(connectionString);
});

// Step 2: Register SQL Store
builder.Services.AddIdempotencyMsSqlStore();

// Step 3: Register Middleware
builder.Services.AddIdempotency();
```

**Service Resolution Chain**:

```
IIdempotencyKeyStore 
  ↓ (injected into)
IdempotencyEndpointFilter
  ↓ (which uses)
IdempotencySqlServerStore
  ↓ (which uses)
IdempotencyDbContext
```

---

## Test Contract

### Integration Test Requirements

**Using TestContainers.MsSql**:

```csharp
[CollectionDefinition("IdempotencyDb Collection")]
public sealed class IdempotencyDbCollection : ICollectionFixture<IdempotencyDbFixture> { }

[Collection("IdempotencyDb Collection")]
public sealed class IdempotencySqlServerStoreTests
{
    private readonly IdempotencyDbFixture _fixture;

    public IdempotencySqlServerStoreTests(IdempotencyDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task IsKeyProcessedAsync_WhenKeyNotExists_ReturnsFalseAndNull()
    {
        // Arrange
        var store = new IdempotencySqlServerStore(
            _fixture.DbContext,
            Options.Create(new IdempotencyMsSqlOptions()),
            _fixture.Logger);

        // Act
        var (processed, response) = await store.IsKeyProcessedAsync("nonexistent-key");

        // Assert
        processed.ShouldBeFalse();
        response.ShouldBeNull();
    }
}
```

**Test Fixture Contract**:

```csharp
public sealed class IdempotencyDbFixture : IAsyncLifetime
{
    private MsSqlContainer _container;
    public IdempotencyDbContext DbContext { get; private set; }
    public ILogger<IdempotencySqlServerStore> Logger { get; private set; }

    public async Task InitializeAsync()
    {
        // Start container
        // Create DbContext with container connection string
        // Run migrations
    }

    public async Task DisposeAsync()
    {
        // Cleanup resources
    }
}
```

---

## Backward Compatibility Contract

### Existing Code Compatibility

**No Breaking Changes**:

- ✅ Existing `IdempotencyDistributedCacheStore` unchanged
- ✅ Existing `[RequireIdempotency]` attribute unchanged
- ✅ DKNet.AspCore.Idempotency requires no modifications
- ✅ Existing endpoints continue to work with distributed cache

**Opt-In to SQL Storage**:

```csharp
// Old code (still works)
builder.Services.AddIdempotency(); // Uses distributed cache by default

// New code (explicit SQL storage)
builder.Services.AddIdempotencyMsSqlStore(); // Uses SQL Server
builder.Services.AddIdempotency();
```

---

## Summary

✅ **Service Registration**: `AddIdempotencyMsSqlStore()` extension method  
✅ **DbContext**: EF Core 10 pattern with primary constructor  
✅ **Configuration**: `IEntityTypeConfiguration<IdempotencyKey>` pattern  
✅ **Store Implementation**: `IdempotencySqlServerStore` implementing `IIdempotencyKeyStore`  
✅ **Migrations**: EF Core code-first with auto-generated SQL  
✅ **Backward Compatible**: No breaking changes to existing code  
✅ **Well-Tested**: Integration tests using TestContainers.MsSql  

---

Generated: January 30, 2026  
Status: ✅ COMPLETE - Ready for Implementation
