# Register the service

Part of [DKNet.Svc.Transformation](index.md). `AddTransformerService(Action<TransformOptions>?)` is the only registration entry point: it builds one `TransformOptions`, registers it as `IOptions<TransformOptions>`, and registers `ITransformerService → TransformerService` as transient.

```csharp
builder.Services.AddTransformerService(options =>
{
    options.GlobalParameters = [new { CompanyName = "Acme Corp" }];
});
```

It returns immediately if `ITransformerService` is already registered — so the **first** call's options win and a later call's configuration delegate is silently ignored.

![services.AddTransformerService checks whether ITransformerService is already registered; if not, it builds TransformOptions, invokes the configure delegate, and registers the transient service — otherwise it returns immediately and the delegate never runs.](diagrams/register-the-service.svg)

This does not show what the registered service does per call — see the package [🔄 How it works](index.md#-how-it-works) — or that registering your own `ITransformerService` implementation before calling `AddTransformerService` makes it return early too, which is how you can replace the service outright.

Tokens resolve against the call's own `parameters` first and fall back to `GlobalParameters` — the place for values shared by every template the app renders (company name, support address). With no `parameters` at all, resolution uses `GlobalParameters` only.

## ⚙️ Configuration reference

| Option | Type | Default | Effect |
|---|---|---|---|
| `GlobalParameters` | `IEnumerable<object>` | `[]` | Fallback resolution sources tried after the call's own `parameters` — see [fill a template from data](fill-a-template-from-data.md). |

There is no `IConfiguration`/`appsettings.json` binding path for `TransformOptions` — it is configured entirely through the delegate passed to `AddTransformerService`.

## 🧱 Where it fits

`AddTransformerService` is the host's one entry point into the package: it owns both the `TransformOptions` the other features read ([token syntax](choose-token-syntax.md), [value formatting](format-resolved-values.md)) and the `ITransformerService` registration the host calls into. See the package [🏗️ Architecture](index.md#-architecture) diagram for how the registered service relates to the host application.

## ⚠️ Gotchas & limits

- **Only the first `AddTransformerService` call configures the service.** Calling it twice from different startup extensions (e.g. a shared library and the app itself) silently drops the second call's options — there is no error or warning.
- **Register your own `ITransformerService` before calling `AddTransformerService`** to replace the service outright; `ITokenExtractor` and `ITokenResolver` have no equivalent seam (their implementations are `internal sealed`, constructed directly by `TransformerService`).
