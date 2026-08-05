# DKNet.AspCore.Idempotency.NpgsqlStore

PostgreSQL persistent storage implementation for DKNet.AspCore.Idempotency.

## Overview

This library provides a PostgreSQL-backed storage implementation for idempotency keys, replacing the default distributed-cache storage. It uses Entity Framework Core 10 with the Npgsql provider, and detects concurrent duplicate inserts via Postgres's own unique-violation error code (`23505`) rather than message inspection.

## Installation

```bash
dotnet add package DKNet.AspCore.Idempotency.NpgsqlStore
```

## Quick Start

```csharp
using DKNet.AspCore.Idempotency.NpgsqlStore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdempotencyWithNpgsqlStore(
    builder.Configuration.GetConnectionString("IdempotencyDb")!,
    options =>
    {
        options.Expiration = TimeSpan.FromHours(24);
        options.ConflictHandling = IdempotentConflictHandling.CachedResult;
    });

var app = builder.Build();

app.MapPost("/orders", CreateOrder).RequiredIdempotentKey();

app.Run();
```

**appsettings.json:**
```json
{
  "ConnectionStrings": {
    "IdempotencyDb": "Host=localhost;Database=MyAppIdempotency;Username=app;Password=changeit"
  }
}
```

## Database Schema

A single `IdempotencyKeys` table, with a unique index (`UX_CompositeKey`) on `CompositeKey` that guarantees exactly one stored entry per idempotency key/endpoint/method combination, and a check constraint (`CK_StatusCode_Valid`) restricting `StatusCode` to the 100-599 range.

## Concurrency Handling

Concurrent requests with the same idempotency key race to insert; the loser's `DbUpdateException` is inspected for `PostgresException.SqlState == PostgresErrorCodes.UniqueViolation` ("23505") and swallowed, so only one entry is ever persisted — no application-level locking required.

## Requirements

- .NET 10.0+
- PostgreSQL 13+
- DKNet.AspCore.Idempotency (automatically included)

## License

MIT License - see LICENSE file for details.
