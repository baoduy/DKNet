# DKNet.AspCore.Idempotency.RedisStore

[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Idempotency.RedisStore.svg)](https://www.nuget.org/packages/DKNet.AspCore.Idempotency.RedisStore/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A Redis-backed idempotency key store for [`DKNet.AspCore.Idempotency`](https://www.nuget.org/packages/DKNet.AspCore.Idempotency/).
Implements the store contract directly against `StackExchange.Redis` — no schema, no migrations, and expiry falls
out of Redis's own TTL.

## Features

- Atomic request reservation via `SET NX` — exactly one concurrent caller per idempotency key proceeds
- SHA-256 hashed, prefixed Redis keys so distinct composite keys never collide
- Native TTL-based expiry for both in-flight reservations and completed responses
- Reuses an existing `IConnectionMultiplexer`/`IDistributedCache`, or registers its own from a connection string

## Installation

```bash
dotnet add package DKNet.AspCore.Idempotency.RedisStore
```

## Quick Start

```csharp
builder.Services.AddIdempotencyWithRedisStore(
    builder.Configuration.GetConnectionString("Redis")!,
    options =>
    {
        options.Expiration = TimeSpan.FromHours(48);
    });
```

## Documentation

Full feature reference: https://github.com/baoduy/DKNet/blob/main/docs/AspNetCore/DKNet.AspCore.Idempotency.RedisStore.md

## License

This project is licensed under the MIT License - see the [LICENSE](https://opensource.org/licenses/MIT) file for
details.

## About

Developed by [Steven Hoang](https://drunkcoding.net).
