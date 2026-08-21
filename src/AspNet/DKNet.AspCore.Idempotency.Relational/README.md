# DKNet.AspCore.Idempotency.Relational

Shared EF Core relational implementation for DKNet.AspCore.Idempotency stores.

## Overview

This library holds the entity, `DbContext`, entity configuration and store logic that
[`DKNet.AspCore.Idempotency.MsSqlStore`](../DKNet.AspCore.Idempotency.MsSqlStore/README.md) and
[`DKNet.AspCore.Idempotency.NpgsqlStore`](../DKNet.AspCore.Idempotency.NpgsqlStore/README.md) build on,
so the two relational providers don't carry the same EF Core code twice. It is not intended to be
referenced directly by applications — install one of the provider packages instead.

## What lives here

- `IdempotencyKeyEntity` — the persisted idempotency row.
- `IdempotencyDbContext` — abstract `DbContext` base wiring the shared entity mapping.
- `IdempotencyKeyConfiguration` — abstract `IEntityTypeConfiguration<IdempotencyKeyEntity>` base; each
  provider supplies its own column type and check-constraint SQL for the parts that differ.
- `IdempotencyRelationalStore<TContext>` — abstract `IIdempotencyKeyStore` implementation: reserve /
  check / complete flow, a per-connection-string migration guard, and an atomic reclaim of expired
  reservations. Each provider supplies its own unique-violation detection.

## License

MIT License - see LICENSE file for details.
