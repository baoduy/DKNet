# DKNet.AspCore.Idempotency.MsSqlStore

[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Idempotency.MsSqlStore.svg)](https://www.nuget.org/packages/DKNet.AspCore.Idempotency.MsSqlStore/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A SQL Server-backed persistent store for `DKNet.AspCore.Idempotency` — idempotency keys and cached
responses survive application restarts, with a race-free reserve-then-check flow under concurrent
duplicate requests.

## Features

- Persists idempotency keys and cached responses in SQL Server via EF Core
- Race-free duplicate handling backed by a database unique index, not application-level locking
- Migrations ship with the package and run automatically on first use
- Configured through the same `IdempotencyOptions` the core package already exposes — no extra options type to learn

## Installation

```bash
dotnet add package DKNet.AspCore.Idempotency.MsSqlStore
```

## Quick Start

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.MsSqlStore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdempotencyWithMsSqlStore(
    builder.Configuration.GetConnectionString("IdempotencyDb")!,
    options =>
    {
        options.Expiration = TimeSpan.FromHours(48);
        options.ConflictHandling = IdempotentConflictHandling.CachedResult;
    });

var app = builder.Build();

app.MapPost("/orders", () => Results.Ok())
    .RequiredIdempotentKey();

app.Run();
```

## Learn more

Full registration details, the SQL Server schema, and gotchas around migrations and concurrency:
[DKNet.AspCore.Idempotency.MsSqlStore docs](https://github.com/baoduy/DKNet/blob/main/docs/AspNetCore/DKNet.AspCore.Idempotency.MsSqlStore.md).

## Requirements

- .NET 10.0+
- SQL Server 2019+ (or Azure SQL Database)

## License

MIT License - see LICENSE file for details.

## About

Developed by [Steven Hoang](https://drunkcoding.net).
