# DKNet.AspCore.Idempotency.MsSqlStore

MS SQL Server persistent storage implementation for DKNet.AspCore.Idempotency.

## Overview

This library provides a SQL Server-backed storage implementation for idempotency keys, replacing or complementing the default distributed cache storage. It uses Entity Framework Core 10 with best practices including primary constructors, required properties, and configuration separation.

## Features

- ✅ **Persistent Storage**: Idempotency keys survive application restarts
- ✅ **EF Core 10**: Uses latest patterns (primary constructors, required DbSet)
- ✅ **Concurrent-Safe**: Database unique constraints prevent race conditions
- ✅ **Configurable Error Handling**: Fail-open or fail-closed on database errors
- ✅ **Automatic Cleanup**: TTL-based expiration (default 24 hours)
- ✅ **Performance Optimized**: Composite indexes for fast lookups

## Installation

```bash
dotnet add package DKNet.AspCore.Idempotency.MsSqlStore
```

## Quick Start

### 1. Configure in Program.cs

```csharp
using DKNet.AspCore.Idempotency.MsSqlStore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register SQL Server idempotency storage
builder.Services.AddIdempotencyMsSqlStore(
    builder.Configuration,
    connectionStringName: "IdempotencyDb",
    options =>
    {
        options.Expiration = TimeSpan.FromHours(24);
        options.FailOpen = false; // Fail-closed by default
    });

// Register idempotency middleware
builder.Services.AddIdempotency();

var app = builder.Build();

// Apply migrations on startup (development only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();
```

### 2. Configure Connection String

**appsettings.json:**
```json
{
  "ConnectionStrings": {
    "IdempotencyDb": "Server=(local);Database=MyAppIdempotency;Trusted_Connection=true;TrustServerCertificate=true;"
  }
}
```

### 3. Create and Apply Migrations

```bash
# Add migration
dotnet ef migrations add InitialIdempotencySchema \
    --project YourProject \
    --context IdempotencyDbContext

# Apply migration
dotnet ef database update --project YourProject
```

### 4. Use Idempotency in Controllers

```csharp
using DKNet.AspCore.Idempotency;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    [HttpPost]
    [RequireIdempotency]  // Enable idempotency
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        // Your business logic here
        var order = await _orderService.CreateAsync(request);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }
}
```

### 5. Client Usage

```csharp
var client = new HttpClient();
var idempotencyKey = Guid.NewGuid().ToString();

var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/api/orders")
{
    Headers = { { "Idempotency-Key", idempotencyKey } },
    Content = new StringContent(JsonSerializer.Serialize(orderData), Encoding.UTF8, "application/json")
};

// First request - processes the order
var response1 = await client.SendAsync(request);

// Second request - returns cached response
var response2 = await client.SendAsync(request);  // Same key = cached response
```

## Configuration Options

### IdempotencyMsSqlOptions

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Expiration` | `TimeSpan` | `24 hours` | How long to keep idempotency keys in the database |
| `FailOpen` | `bool` | `false` | If true, continues on DB errors (fail-open); if false, blocks (fail-closed) |
| `JsonSerializerOptions` | `JsonSerializerOptions` | Web defaults | Customizes JSON serialization for cached responses |

### Example Configuration

```csharp
builder.Services.AddIdempotencyMsSqlStore(
    connectionString,
    options =>
    {
        // Expire keys after 48 hours
        options.Expiration = TimeSpan.FromHours(48);
        
        // Allow requests through on database failures (fail-open)
        options.FailOpen = true;
        
        // Customize JSON serialization
        options.JsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    });
```

## Database Schema

The library creates a single table `IdempotencyKeys` with the following structure:

```sql
CREATE TABLE [dbo].[IdempotencyKeys] (
    [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [Key] NVARCHAR(128) NOT NULL,
    [Route] NVARCHAR(256) NOT NULL,
    [HttpMethod] NVARCHAR(10) NOT NULL,
    [StatusCode] INT NOT NULL,
    [ResponseBody] NVARCHAR(MAX) NULL,
    [ContentType] NVARCHAR(256) NULL,
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [ExpiresAt] DATETIME2(7) NOT NULL,
    [IsProcessed] BIT NOT NULL,
    [ProcessingCompletedAt] DATETIME2(7) NULL,
    
    CONSTRAINT [PK_IdempotencyKeys] PRIMARY KEY ([Id]),
    CONSTRAINT [UX_IdempotencyKey_Composite] UNIQUE ([Route], [HttpMethod], [Key])
);
```

### Indexes

- **Unique Composite**: `(Route, HttpMethod, Key)` - Prevents duplicate processing
- **ExpiresAt**: Fast cleanup of expired keys
- **Route + CreatedAt**: Dashboard/monitoring queries

## Error Handling

### Fail-Closed (Default - Production Safe)

```csharp
options.FailOpen = false;  // Block on database errors
```

**Behavior**: If the database is unavailable, requests will fail with 500 status code.
**Use Case**: Production systems where idempotency correctness is critical.

### Fail-Open (High Availability)

```csharp
options.FailOpen = true;  // Continue on database errors
```

**Behavior**: If the database is unavailable, requests proceed without idempotency checks.
**Use Case**: Systems where availability is more important than idempotency guarantees.

## Concurrency Handling

The library handles concurrent duplicate requests safely:

```
Request A: INSERT key → Success
Request B: INSERT same key → Unique constraint violation
         → Caught and logged as debug
         → Query returns A's cached response
```

The database unique constraint (`UX_IdempotencyKey_Composite`) ensures atomicity without application-level locking.

## Performance

| Operation | Latency | Notes |
|-----------|---------|-------|
| Check if key exists | ~5-10ms | Uses unique index |
| Store processed key | ~15-20ms | Includes network round-trip |
| Dashboard query | ~20-50ms | With pagination |

**Scaling**: Handles 1000+ requests/second with proper indexing.

## Troubleshooting

### Connection String Issues

```
InvalidOperationException: Connection string 'IdempotencyDb' not found
```

**Solution**: Ensure `appsettings.json` contains the connection string.

### Migration Issues

```
Could not apply migrations
```

**Solution**: Ensure SQL Server is running and accessible:

```bash
# Test connection
sqlcmd -S localhost -U sa -P YourPassword

# Apply migrations manually
dotnet ef database update --project YourProject
```

### Unique Constraint Violations in Logs

```
Idempotency key already processed (expected in concurrent scenario)
```

**Status**: This is NORMAL behavior for concurrent duplicate requests. The first request wins, subsequent ones use the cached response.

## Requirements

- .NET 10.0+
- SQL Server 2019+ (or Azure SQL Database)
- DKNet.AspCore.Idempotency (automatically included)

## License

MIT License - see LICENSE file for details.

## Support

For issues and questions, please open an issue on the [GitHub repository](https://github.com/baoduy/DKNet).
