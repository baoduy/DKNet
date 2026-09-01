# DKNet.EfCore.Hooks

[![NuGet](https://img.shields.io/nuget/v/DKNet.EfCore.Hooks)](https://www.nuget.org/packages/DKNet.EfCore.Hooks/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.EfCore.Hooks)](https://www.nuget.org/packages/DKNet.EfCore.Hooks/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

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
            if (entry.OriginalState != EntityState.Added || entry.Entity is not IAuditedProperties)
                continue;

            // IAuditedProperties exposes get-only properties by design; write through the tracked entry.
            entry.Entry.Property(nameof(IAuditedProperties.CreatedBy)).CurrentValue = currentUser.UserId;
            entry.Entry.Property(nameof(IAuditedProperties.CreatedOn)).CurrentValue = DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }
}

// Registration
services.AddDbContextWithHook<AppDbContext>((provider, options) =>
    options.UseSqlServer(connectionString));

services.AddHook<AppDbContext, AuditStampHook>();
```

## Customisation reference

There is no options object — behaviour is entirely a function of what you register.

| Knob | Default | How to change it |
|---|---|---|
| Which `DbContext` types run hooks | none until registered | `AddDbContextWithHook<TDbContext>(...)`, or `options.UseHooks<TDbContext>(provider)` when you register the context yourself |
| Which hooks run for a `DbContext` | none | `AddHook<TDbContext, THook>()`, once per `(TDbContext, THook)` pair; a repeat registration is a no-op |
| Hook execution order | DI registration order, all before-hooks then all after-hooks | register the hooks in the order you need |
| Hook lifetime | scoped, keyed by the `DbContext` type's full name | not configurable |
| `HookRunnerInterceptor` lifetime | singleton, keyed by the `DbContext` type's full name | not configurable |
| `AddDbContextWithHook` — `contextLifetime` | `ServiceLifetime.Scoped` | pass a different `ServiceLifetime` |
| `AddDbContextWithHook` — `optionLifetime` | `ServiceLifetime.Scoped` | pass a different `ServiceLifetime` |
| Hooks enabled | enabled | `using (dbContext.DisableHooks()) { … }` — reference-counted per `DbContext` CLR type, so nested scopes are safe |

Hooks are skipped for a save when the context is inside a `DisableHooks()` scope, or when the snapshot contains no
`Added`/`Modified`/`Deleted` entries. After-save hooks never run when the save itself threw.

Full documentation: https://github.com/baoduy/DKNet/blob/main/docs/EfCore/DKNet.EfCore.Hooks.md
