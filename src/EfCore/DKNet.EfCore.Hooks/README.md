# DKNet.EfCore.Hooks

[![NuGet](https://img.shields.io/nuget/v/DKNet.EfCore.Hooks)](https://www.nuget.org/packages/DKNet.EfCore.Hooks/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.EfCore.Hooks)](https://www.nuget.org/packages/DKNet.EfCore.Hooks/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../LICENSE)

A pluggable before/after-`SaveChanges` interceptor pipeline for EF Core. Implement a small hook interface instead of overriding `SaveChangesAsync`, and let independent concerns (audit stamping, domain events, data ownership, …) share the same `DbContext` without knowing about each other.

## Installation

```bash
dotnet add package DKNet.EfCore.Hooks
```

## Features

- `IBeforeSaveHookAsync` / `IAfterSaveHookAsync` / `IHookAsync` — implement the phase(s) you need against a shared `SnapshotContext` of changed entities
- `HookAsync` base class with no-op virtual methods to override only what you use
- `AddDbContextWithHook<TDbContext>` — registers the `DbContext` with the hook interceptor wired in
- `AddHook<TDbContext, THook>()` — register a hook per `DbContext` type; idempotent, inherited by derived `DbContext` types
- `dbContext.DisableHooks()` — reference-counted, disposable scope to suppress all hooks (e.g. during seeding/migrations)
- Used internally by `DKNet.EfCore.Events`, `DKNet.EfCore.AuditLogs`, and `DKNet.EfCore.DataAuthorization`

## Quick start

```csharp
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.EntityFrameworkCore;

public sealed class AuditStampHook(ICurrentUserService currentUser) : IBeforeSaveHookAsync
{
    public Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entities)
        {
            if (entry.Entity is IAuditedProperties audited && entry.OriginalState == EntityState.Added)
                audited.CreatedBy = currentUser.UserId;
        }

        return Task.CompletedTask;
    }
}

// Registration
services.AddDbContextWithHook<AppDbContext>((provider, options) =>
    options.UseSqlServer(connectionString));

services.AddHook<AppDbContext, AuditStampHook>();
```

Full documentation: https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Hooks.md
