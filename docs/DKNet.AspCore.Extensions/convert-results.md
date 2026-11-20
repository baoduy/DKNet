# Convert FluentResults to HTTP results

Part of [DKNet.AspCore.Extensions](index.md). Use the public `Response` overloads for handler results and hand-written endpoints:

```csharp
using DKNet.AspCore.Extensions.Responses;
using FluentResults;
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/ok", () => Result.Ok("value").Response());
app.MapGet("/bad", () => Result.Fail("Refused").Response());
await app.RunAsync();
```

![Response returns a success result immediately or defers failed-result shaping until execution resolves the host options.](diagrams/result-response.svg)

| Input | `isCreated` | Output |
|---|---|---|
| Valued success, non-null | false | JSON value, 200. |
| Valued success, null | false | Empty 200. |
| Valued success | true | 201 with literal location `/` and value. |
| `IResultBase` success | false / true | Empty 200 / 201. |
| Either failure | either | Problem response, normally 400; 404 if any error is `NotFoundError`. |

Failure output resolves optional host `ErrorResponseOptions` when the result executes. The standard body has `title: Error`, status name in `type`, trace ID and a list of `ErrorItem` under `errors`. The internal problem builder is not a public bypass. See [one error-response setting](shape-error-responses.md#one-error-response-setting).

None — no DI registration needed; `Response()` is an extension method you call directly. Register [Apply one error-response setting](shape-error-responses.md)'s `AddErrorResponses` only if you want its optional, shared failure-shaping behaviour.

## ⚙️ Configuration reference

None — `Response()` takes no options of its own. It resolves the optional `ErrorResponseOptions` singleton registered by [Apply one error-response setting](shape-error-responses.md) from `HttpContext.RequestServices` when a failure executes; that page owns the full `ErrorResponseOptions` reference and its override point.

## 🧱 Where it fits

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| [Dispatch HTTP requests through SlimMessageBus](dispatch-bus-requests.md)'s mappers | DKNet.AspCore.Extensions (same package) | Call `.Response()` (and the `isCreated` overload) on the `IMessageBus` dispatch result to build every mapper's success and failure responses. |

None outside this package — no other DKNet package calls `Response()`.

`Response()` recognizes [`NotFoundError`](../DKNet.SlimBus.Extensions/send-commands.md), a `FluentResults.Error` type declared in `DKNet.SlimBus.Extensions`, and maps it to HTTP 404 instead of the default 400.

**Entry point in your app:** calling `.Response()` on a `FluentResults.Result`/`Result<T>` returned from a minimal-API route handler.

## ⚠️ Gotchas & limits

- Null input throws `ArgumentNullException`.
- The generic created-response location is `/`; it does not infer the new resource URL.
- Retired overloads accepting `ErrorResponseOptions` directly and the old public `ToProblemDetails` conversion path are not current APIs.
