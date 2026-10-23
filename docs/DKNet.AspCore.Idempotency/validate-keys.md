# Validate client keys

Part of [DKNet.AspCore.Idempotency](index.md). A protected endpoint reads the first value of the configured header and checks presence, effective length, and pattern before asking the store to reserve it.

```csharp
using DKNet.AspCore.Idempotency;
using Microsoft.Extensions.DependencyInjection;
var services = new ServiceCollection();
services.AddIdempotentKey(o => o.MaxIdempotencyKeyLength = 500);
```

![The filter reads the first header, validates presence and length, then matches the bounded regular expression before accessing the store.](diagrams/validate-key.svg)

`IdempotentKeyInfo.IsValid` returns a FluentResults result; the endpoint filter converts a failure into a 400 problem response. The regular expression has a fixed 100 ms timeout. A timeout is the same refusal as a mismatch.

## ⚙️ Configuration reference

| Option | Type | Default | Effect |
|---|---|---|---|
| `IdempotencyHeaderKey` | `string` | `X-Idempotency-Key` | Header to read. |
| `MaxIdempotencyKeyLength` | `int` | 255 | Accepted length, capped at 500 regardless of this value. |
| `IdempotencyKeyPattern` | `string` | `^[a-zA-Z0-9\-_]+$` | Compiled on assignment with a 100 ms match timeout. |

`AddIdempotentKey` validates a nonblank header and pattern, and a length of at least 1, on host start. Invalid regex syntax throws when assigned.

## ⚠️ Gotchas & limits

- Raising the configured limit above 500 does not admit longer keys. The cap matches the relational key column.
- A pattern timeout bounds a match; it does not make an expensive pattern cheap.
- Validation does not compare request bodies. Reusing the same composite key with a different payload still counts as a duplicate.
