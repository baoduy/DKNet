# Quick Start Guide: MS SQL Storage for Idempotency Keys

**Date**: January 30, 2026  
**Feature**: 001-mssql-idempotency-store  
**Version**: EF Core 10.0.2 | .NET 10.0  
**Status**: Phase 1 - Design Complete

---

## Overview

This guide provides step-by-step instructions for setting up and using the MS SQL storage backend for DKNet.AspCore.Idempotency. The implementation uses EF Core 10 with best practices including primary constructors, required DbSets, and configuration separation.

---

## Installation

### 1. Add NuGet Package

```bash
dotnet add package DKNet.EfCore.Idempotency
# Which depends on:
# - DKNet.AspCore.Idempotency
# - Microsoft.EntityFrameworkCore.SqlServer (10.0.2+)
```

### 2. Create IdempotencyDbContext

The DbContext uses EF Core 10's primary constructor pattern:

```csharp
// Data/IdempotencyDbContext.cs
namespace MyApp.Data;

/// <summary>
///     Entity Framework Core DbContext for idempotency storage.
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

**EF Core 10 Features Used**:
- ✅ **Primary Constructor**: `(DbContextOptions<IdempotencyDbContext> options)`
- ✅ **Required DbSet**: Eliminates null suppression `= null!`
- ✅ **ApplyConfigurationsFromAssembly**: Auto-discovers IEntityTypeConfiguration implementations

### 3. Configure Entity Mapping (EF Core 10 Pattern)

Separate configuration from DbContext using `IEntityTypeConfiguration`:

```csharp
// Data/Configurations/IdempotencyKeyConfiguration.cs
namespace MyApp.Data.Configurations;

/// <summary>
///     Entity configuration for IdempotencyKey.
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
            .HasColumnType("nvarchar(10) collate SQL_Latin1_General_CP1_CS_AS");

        builder.Property(k => k.StatusCode)
            .IsRequired();

        builder.Property(k => k.ResponseBody)
            .HasMaxLength(1048576)
            .HasColumnType("nvarchar(max)");

        builder.Property(k => k.ContentType)
            .HasMaxLength(256);

        builder.Property(k => k.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()")
            .HasPrecision(7)
            .ValueGeneratedOnAdd();

        builder.Property(k => k.ExpiresAt)
            .HasPrecision(7);

        builder.Property(k => k.ProcessingCompletedAt)
            .HasPrecision(7);

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

**Benefits of IEntityTypeConfiguration Pattern**:
- ✅ **Separation of Concerns**: Configuration is separate from DbContext
- ✅ **Testability**: Each configuration can be unit tested independently
- ✅ **Maintainability**: Easy to find and modify entity mappings
- ✅ **Convention-Based Discovery**: ApplyConfigurationsFromAssembly finds all implementations automatically

### 4. Register Services in Program.cs

```csharp
// Program.cs
var builder = WebApplicationBuilder.CreateBuilder(args);

// Configure the DbContext
builder.Services.AddDbContext<IdempotencyDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("IdempotencyDb")
        ?? throw new InvalidOperationException("Connection string 'IdempotencyDb' not found.");
    
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.MigrationsAssembly("MyApp");
    });
});

// Register idempotency with SQL Server storage
builder.Services.AddIdempotencyMsSqlStore(options =>
{
    options.Expiration = TimeSpan.FromHours(24);
    options.FailOpen = false; // Fail-closed: block on DB error
});

// Register the idempotency middleware
builder.Services.AddIdempotency();

var app = builder.Build();

// Apply migrations on startup (optional, for demo)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();
```

### 5. Create Initial Migration

```bash
# Generate initial migration
dotnet ef migrations add InitialIdempotencySchema \
    --project MyApp \
    --startup-project MyApp

# Review the generated migration file in Migrations/20260130_InitialIdempotencySchema.cs

# Apply migration to database
dotnet ef database update --project MyApp
```

**Generated Migration Example**:

```csharp
// Migrations/20260130_InitialIdempotencySchema.cs
namespace MyApp.Migrations;

/// <inheritdoc/>
public partial class InitialIdempotencySchema : Migration
{
    /// <inheritdoc/>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "IdempotencyKeys",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Route = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                HttpMethod = table.Column<string>(type: "nvarchar(10) collate SQL_Latin1_General_CP1_CS_AS", 
                    maxLength: 10, nullable: false),
                StatusCode = table.Column<int>(type: "int", nullable: false),
                ResponseBody = table.Column<string>(type: "nvarchar(max)", maxLength: 1048576, nullable: true),
                ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false, 
                    defaultValueSql: "GETUTCDATE()"),
                ExpiresAt = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                ProcessingCompletedAt = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IdempotencyKeys", x => x.Id);
                table.CheckConstraint("CK_StatusCode_Valid", "[StatusCode] BETWEEN 100 AND 599");
                table.CheckConstraint("CK_ExpiresAt_Greater_Than_CreatedAt", "[ExpiresAt] > [CreatedAt]");
                table.UniqueConstraint("UX_IdempotencyKey_Composite", x => new { x.Route, x.HttpMethod, x.Key });
            });

        migrationBuilder.CreateIndex(
            name: "IX_IdempotencyKeys_ExpiresAt",
            table: "IdempotencyKeys",
            column: "ExpiresAt");

        migrationBuilder.CreateIndex(
            name: "IX_IdempotencyKeys_Route_CreatedAt",
            table: "IdempotencyKeys",
            columns: new[] { "Route", "CreatedAt" });
    }

    /// <inheritdoc/>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "IdempotencyKeys");
    }
}
```

### 6. Configure appsettings.json

```json
{
  "ConnectionStrings": {
    "IdempotencyDb": "Server=(local);Database=MyAppIdempotency;Trusted_Connection=true;TrustServerCertificate=true;"
  },
  "Idempotency": {
    "Enabled": true,
    "Expiration": "24:00:00",
    "FailOpen": false
  }
}
```

---

## Usage

### Adding Idempotency to an Endpoint

```csharp
// Controllers/OrdersController.cs
using DKNet.AspCore.Idempotency;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController : ControllerBase
{
    /// <summary>
    ///     Creates a new order with idempotency key support.
    /// </summary>
    /// <remarks>
    ///     Include the 'Idempotency-Key' header to enable idempotent processing.
    ///     Duplicate requests with the same key will return the cached response from the first request.
    /// </remarks>
    [HttpPost]
    [RequireIdempotency] // Enable idempotency on this endpoint
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            Amount = request.Amount,
            CreatedAt = DateTime.UtcNow
        };

        // Your business logic here
        await _orderService.CreateAsync(order);

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    /// <summary>
    ///     Retrieves an order by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(Guid id)
    {
        var order = await _orderService.GetByIdAsync(id);
        if (order == null)
            return NotFound();

        return Ok(order);
    }
}
```

### Client Usage Example

```csharp
// Client making idempotent requests
using var client = new HttpClient();

var idempotencyKey = Guid.NewGuid().ToString();

var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/api/orders")
{
    Headers =
    {
        { "Idempotency-Key", idempotencyKey }
    },
    Content = new StringContent(JsonSerializer.Serialize(new { customerId = 123, amount = 99.99m }),
        Encoding.UTF8, "application/json")
};

// First request - processes the order
var response1 = await client.SendAsync(request);
var content1 = await response1.Content.ReadAsStringAsync();
// Status: 201 Created

// Second request (duplicate) - returns cached response
var response2 = await client.SendAsync(request); // Same idempotency key
var content2 = await response2.Content.ReadAsStringAsync();
// Status: 201 Created (cached from first request)

Console.WriteLine($"Request 1: {response1.StatusCode}");
Console.WriteLine($"Request 2: {response2.StatusCode}");
Console.WriteLine($"Responses identical: {content1 == content2}");
```

---

## Querying Idempotency Keys

### Using IdempotencyDbContext Directly

```csharp
// Service to inspect idempotency keys
public sealed class IdempotencyKeyQueryService
{
    private readonly IdempotencyDbContext _dbContext;

    public IdempotencyKeyQueryService(IdempotencyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    ///     Gets all idempotency keys for a specific route.
    /// </summary>
    public async Task<List<IdempotencyKey>> GetByRouteAsync(string route)
    {
        return await _dbContext.IdempotencyKeys
            .AsNoTracking()
            .Where(k => k.Route == route)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets active (non-expired) idempotency keys.
    /// </summary>
    public async Task<List<IdempotencyKey>> GetActiveKeysAsync()
    {
        return await _dbContext.IdempotencyKeys
            .AsNoTracking()
            .Where(k => k.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets expired idempotency keys for cleanup.
    /// </summary>
    public async Task<int> CleanupExpiredKeysAsync()
    {
        return await _dbContext.IdempotencyKeys
            .Where(k => k.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync();
    }
}
```

---

## Configuration Options

### IdempotencyMsSqlOptions

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Expiration` | `TimeSpan` | `24 hours` | How long to keep idempotency keys in the database |
| `FailOpen` | `bool` | `false` | If true, continues on DB errors (fail-open); if false, blocks (fail-closed) |
| `JsonSerializerOptions` | `JsonSerializerOptions` | Default | Customizes JSON serialization for cached responses |

### Configuration Example

```csharp
builder.Services.AddIdempotencyMsSqlStore(options =>
{
    // Expire keys after 48 hours
    options.Expiration = TimeSpan.FromHours(48);
    
    // Allow requests through on database failures
    options.FailOpen = true;
    
    // Customize JSON serialization (case-insensitive, ignore null)
    options.JsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
});
```

---

## EF Core 10 Best Practices Used

### 1. Primary Constructors

```csharp
// ✅ EF Core 10 / C# 13 - Clean, modern syntax
public sealed class IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options) 
    : DbContext(options)
{
    // ...
}

// ❌ Old style (pre-EF Core 10)
public sealed class IdempotencyDbContext : DbContext
{
    public IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options) 
        : base(options) { }
}
```

### 2. Required DbSet Properties

```csharp
// ✅ EF Core 10 - No null suppression needed
public required DbSet<IdempotencyKey> IdempotencyKeys { get; init; }

// ❌ Old style (pre-EF Core 10)
public DbSet<IdempotencyKey> IdempotencyKeys { get; set; } = null!;
```

### 3. Configuration Separation (IEntityTypeConfiguration)

```csharp
// ✅ EF Core Best Practice - Separate, testable configuration
internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder) { }
}

// Then in DbContext:
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyDbContext).Assembly);
}

// ❌ Old style (configuration mixed in DbContext)
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    var entity = modelBuilder.Entity<IdempotencyKey>();
    // ... all configuration here
}
```

### 4. Case-Sensitive Collation

```csharp
// ✅ EF Core 10 - Explicit collation for HTTP method accuracy
builder.Property(k => k.HttpMethod)
    .HasColumnType("nvarchar(10) collate SQL_Latin1_General_CP1_CS_AS");
```

---

## Troubleshooting

### Connection String Issues

```
InvalidOperationException: Connection string 'IdempotencyDb' not found
```

**Solution**: Ensure `appsettings.json` contains the connection string:

```json
{
  "ConnectionStrings": {
    "IdempotencyDb": "Server=localhost;Database=MyAppIdempotency;Trusted_Connection=true;"
  }
}
```

### Migration Issues

```
Could not apply migrations because the 'idempotency' schema does not exist
```

**Solution**: Create the schema first:

```sql
CREATE SCHEMA idempotency;
GO
```

Or configure migrations to use default schema:

```csharp
builder.Services.AddDbContext<IdempotencyDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "idempotency");
    });
});
```

---

## Performance Considerations

### Indexing Strategy

The implementation includes three indexes optimized for common queries:

1. **Unique Composite Index** `(Route, HttpMethod, Key)` - Fast duplicate detection
2. **ExpiresAt Index** - Fast cleanup queries
3. **Route + CreatedAt Index** - Fast dashboard/monitoring queries

### Query Performance

| Operation | Expected Time | Notes |
|-----------|---------------|-------|
| Check if key processed | ~5-10ms | Uses unique index, very fast |
| Store processed key | ~15-20ms | Includes network round-trip |
| Cleanup expired keys | Variable | Depends on table size, uses index |
| Dashboard query (paginated) | ~20-50ms | Uses route+createdAt index |

### Monitoring

```csharp
// Monitor slow queries
var slowQueries = await _dbContext.IdempotencyKeys
    .FromSqlInterpolated($@"
        SELECT * FROM sys.dm_exec_requests 
        WHERE command LIKE '%IdempotencyKey%'
        AND status = 'running'
        AND datediff(ms, start_time, getdate()) > 1000")
    .ToListAsync();
```

---

## Summary

✅ **Installation**: Add NuGet package and register services  
✅ **DbContext**: Use EF Core 10 primary constructor pattern  
✅ **Configuration**: Separate entity configuration with IEntityTypeConfiguration  
✅ **Migrations**: Generate and apply with `dotnet ef` CLI  
✅ **Usage**: Add `[RequireIdempotency]` attribute to endpoints  
✅ **Querying**: Use IdempotencyDbContext for monitoring and cleanup  

**Next Steps**:
1. Follow the setup instructions above
2. Run migrations to create the database schema
3. Add `[RequireIdempotency]` to your endpoints
4. Test with duplicate requests (same Idempotency-Key header)
5. Monitor idempotency keys via the query service

---

Generated: January 30, 2026  
Status: ✅ COMPLETE - Ready for Implementation
