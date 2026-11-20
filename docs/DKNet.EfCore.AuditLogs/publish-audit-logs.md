# Publish audit batches

Part of [DKNet.EfCore.AuditLogs](index.md). The Quick Start publisher implements `IAuditLogPublisher.PublishAsync`. To register several sinks, replace its one-call publisher registration with:

```csharp
services.AddEfCoreAuditHook<DemoDb>();
services.AddKeyedScoped<IAuditLogPublisher, ConsolePublisher>(typeof(DemoDb).FullName!);
```

![After a successful EF save the hook removes the in-memory batch, awaits each keyed publisher, and logs an individual failure before continuing.](diagrams/publish-audit-logs.svg)

Add another keyed scoped registration for each additional publisher implementation. Resolve the registered sinks with `scope.ServiceProvider.GetAuditLogPublishers<DemoDb>()` when needed. `AddEfCoreAuditLogs<TDbContext, TPublisher>()` combines publisher and hook registration and ignores a duplicate of the same publisher for the same key.

## ⚙️ Configuration reference

| Override point | How you customise it |
|---|---|
| `IAuditLogPublisher` | Implement `PublishAsync(IEnumerable<AuditLogEntry>, CancellationToken)` and register it keyed scoped under `typeof(TDbContext).FullName!` with `AddKeyedScoped<IAuditLogPublisher, TPublisher>(...)`, or in one call with `AddEfCoreAuditLogs<TDbContext, TPublisher>()`. |

| Registration | Lifetime / default | Effect |
|---|---|---|
| `AddEfCoreAuditHook<TDbContext>()` | Scoped hook | Attach `EfCoreAuditHook` to the context without a publisher. |
| `AddKeyedScoped<IAuditLogPublisher, TPublisher>(key)` | Scoped, keyed by context type name | Add one more sink; call it once per publisher implementation. |
| `AddEfCoreAuditLogs<TDbContext, TPublisher>()` | Scoped publisher + hook | Combine the two calls above; a duplicate of the same publisher for the same key is ignored. |

## 🧱 Where it fits

None — no other DKNet package calls `IAuditLogPublisher`; you implement and register it yourself. The [package architecture](index.md#-architecture) places the publisher adapter at the host boundary: your implementation owns persistence, transport and retention, while this package owns capture and invocation.

**Entry point in your app:** your `IAuditLogPublisher` implementation, registered as shown above.

## ⚠️ Gotchas & limits

- Publishing exceptions, including cancellation exceptions from a publisher, are caught per sink. The save can return successfully with a lost audit batch. There is no retry or outbox here.
- The structured failure message includes publisher type, entity names and entry count, rather than serializing the batch; the exception is still supplied to the logger.
- Publisher lookup uses the runtime context type name. Register against the concrete context type used at runtime.
- Do not promise atomicity between entity persistence and an external audit sink; an application requiring that needs a transactional design of its own.
