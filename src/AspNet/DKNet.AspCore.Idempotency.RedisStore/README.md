# DKNet.AspCore.Idempotency.RedisStore

Redis persistent storage implementation for DKNet.AspCore.Idempotency.

## Overview

This library provides a Redis-backed storage implementation for idempotency keys, replacing the default distributed cache storage with an atomic store based on StackExchange.Redis.

## Features

- ✅ **Atomic Reservation**: `SET NX` guarantees only one caller reserves a key
- ✅ **Persistent Storage**: Idempotency keys survive as long as configured in Redis
- ✅ **Concurrent-Safe**: No check-then-act race window
- ✅ **Fast Lookups**: In-memory speed for key existence checks

## Installation

```bash
dotnet add package DKNet.AspCore.Idempotency.RedisStore
```

## Quick Start

### 1. Configure in Program.cs

```csharp
using DKNet.AspCore.Idempotency.RedisStore;

var builder = WebApplication.CreateBuilder(args);

// Register Redis idempotency storage
builder.Services.AddIdempotencyWithRedisStore(
    builder.Configuration.GetConnectionString("Redis")!,
    options =>
    {
        options.Expiration = TimeSpan.FromHours(24);
    });

var app = builder.Build();
app.Run();
```

### 2. Configure Connection String

**appsettings.json:**
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

### 3. Use Idempotency in Endpoints

```csharp
using DKNet.AspCore.Idempotency;

app.MapPost("/orders", CreateOrder)
    .RequiredIdempotentKey();
```

## Configuration Options

### IdempotencyOptions

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Expiration` | `TimeSpan` | `4 hours` | How long to keep completed idempotency keys in Redis |
| `InFlightReservationTimeout` | `TimeSpan` | `30 seconds` | How long an in-flight reservation placeholder is honoured |
| `JsonSerializerOptions` | `JsonSerializerOptions` | Camel case | Customizes JSON serialization for cached responses |

### Example Configuration

```csharp
builder.Services.AddIdempotencyWithRedisStore(
    connectionString,
    options =>
    {
        options.Expiration = TimeSpan.FromHours(48);
        options.InFlightReservationTimeout = TimeSpan.FromMinutes(1);
    });
```

## Using an Existing Connection Multiplexer

If your application already configures `IConnectionMultiplexer`, pass it directly:

```csharp
builder.Services.AddIdempotencyRedisStore(existingMultiplexer);
builder.Services.AddIdempotentKey<IdempotencyRedisStore>();
```

## Concurrency Handling

The library handles concurrent duplicate requests safely using Redis `SET NX`:

```
Request A: SET key NX → Success
Request B: SET key NX → Failure
Request B: GET key → Returns A's reservation or cached response
```

The atomic `SET NX` operation eliminates the check-then-act race window present in the default distributed-cache store.

## Requirements

- .NET 10.0+
- Redis 5.0+ (or Redis-compatible cache such as Azure Cache for Redis)
- DKNet.AspCore.Idempotency (automatically included)

## License

MIT License - see LICENSE file for details.

## Support

For issues and questions, please open an issue on the [GitHub repository](https://github.com/baoduy/DKNet).
