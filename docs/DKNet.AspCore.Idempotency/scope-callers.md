# Isolate keys by caller

Part of [DKNet.AspCore.Idempotency](index.md). The storage identity is `Scope:Method:Endpoint:IdempotentKey`. The filter uppercases method and route template, choosing `RoutePattern.RawText`, then route diagnostics metadata, then request path. Route parameter values and request bodies are not added to that identity.

```csharp
using DKNet.AspCore.Idempotency;
using Microsoft.Extensions.DependencyInjection;
var services = new ServiceCollection();
services.AddIdempotentKey(o =>
    o.KeyScopeResolver = ctx => ctx.User.FindFirst("tenant_id")?.Value);
```

![Caller scope falls through authenticated identifier, optional Authorization HMAC and optional IP before using an empty scope.](diagrams/scope-chain.svg)

With no custom resolver, `IdempotencyKeyScopeResolver.Resolve` chooses the authenticated `ClaimTypes.NameIdentifier` (`user:`), then an Authorization-header HMAC-SHA256 digest (`auth:`) if a secret is configured, then remote IP (`ip:`) if enabled, then an empty scope. A custom delegate bypasses that chain; a null result becomes empty.

## ⚙️ Configuration reference

| Option | Type | Default | Effect |
|---|---|---|---|
| `KeyScopeResolver` | `Func<HttpContext, string?>?` | null | Replaces the entire built-in chain. |
| `ScopeHmacSecret` | `string?` | null | Enables the Authorization fallback; non-null whitespace fails startup validation. |
| `IncludeClientIpInScope` | `bool` | false | Enables IP as the last built-in fallback. |

## ⚠️ Gotchas & limits

- Anonymous callers share a scope unless the host supplies another identity. Choose a scope from trusted host data.
- Calls to different IDs on the same route template share the endpoint portion of the key. Give distinct operations distinct client keys.
- `SafeKey` is for display and logging. It replaces CR/LF with spaces and removes other controls and Unicode line separators; storage uses `CompositeKey`.
- Changing a scope secret changes the derived identity. The raw Authorization header is not the stored key.
