# DKNet.AspCore.Idempotency

[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Idempotency.svg)](https://www.nuget.org/packages/DKNet.AspCore.Idempotency/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

An ASP.NET Core minimal-API endpoint filter that makes mutating endpoints safe to retry: requests carrying the same
idempotency key are processed once, with duplicates either rejected with `409 Conflict` or replayed from cache.

## Features

- Endpoint filter (`RequiredIdempotentKey()`) that enforces an idempotency key on any minimal API route
- Composite key validation (presence, length, format) with automatic `400 Bad Request` responses
- Two duplicate-request strategies: `409 Conflict` (default) or transparent cached-response replay
- Caller scope isolation (authenticated user, HMAC'd `Authorization` header, or client IP) so the same key from
  different callers never collides — or supply your own resolver
- Pluggable storage via `IIdempotencyKeyStore`, with a built-in `IDistributedCache`-backed store out of the box
- Configurable which HTTP status codes get cached, cache key prefix, and result expiration

## Installation

```bash
dotnet add package DKNet.AspCore.Idempotency
```

## Quick Start

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDistributedMemoryCache();
builder.Services.AddIdempotentKey<IdempotencyDistributedCacheStore>();

var app = builder.Build();

app.MapPost("/orders", CreateOrder)
    .RequiredIdempotentKey();

await app.RunAsync();
```

Clients call `POST /orders` with an `X-Idempotency-Key` header; a retried request with the same key never
re-executes `CreateOrder`.

For multi-instance production traffic, swap the built-in store for an atomic one from the ecosystem —
`DKNet.AspCore.Idempotency.MsSqlStore`, `DKNet.AspCore.Idempotency.NpgsqlStore`, or
`DKNet.AspCore.Idempotency.RedisStore`.

## Migration — namespace changes in this release

Root types were grouped into concern folders; the namespace of each moved type now ends
with its folder name. This is an import-only source break: no type was renamed, removed,
resignatured, or had its behaviour changed — update the `using` line and you're done.

| Type | Old namespace | New namespace |
|---|---|---|
| `IdempotencyEndpointFilter` (incl. `RequiredIdempotentKey()`), `IdempotencyKeyScopeResolver`, `IdempotentKeyInfo` | `DKNet.AspCore.Idempotency` | `DKNet.AspCore.Idempotency.Filtering` |
| `CachedResponse` | `DKNet.AspCore.Idempotency` | `DKNet.AspCore.Idempotency.Store` (joins the existing `IIdempotencyKeyStore`/`IdempotencyDistributedCacheStore`) |

`IdempotencySetup` (registration point) and `IdempotencyOptions`/`IdempotentConflictHandling`
(the configuration surface) stay at `DKNet.AspCore.Idempotency`.

## Documentation

Full feature guide, configuration reference, and store comparison:
https://github.com/baoduy/DKNet/blob/main/docs/AspNetCore/DKNet.AspCore.Idempotency.md

## License

This project is licensed under the MIT License - see the [LICENSE](https://opensource.org/licenses/MIT) file for
details.

## About

Developed by [Steven Hoang](https://drunkcoding.net).
