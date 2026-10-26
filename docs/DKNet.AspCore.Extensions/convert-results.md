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

## ⚠️ Gotchas & limits

- Null input throws `ArgumentNullException`.
- The generic created-response location is `/`; it does not infer the new resource URL.
- Retired overloads accepting `ErrorResponseOptions` directly and the old public `ToProblemDetails` conversion path are not current APIs.
