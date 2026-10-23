# Apply one error-response setting

Part of [DKNet.AspCore.Extensions](index.md). Register `AddErrorResponses` to shape failed commands, FluentValidation refusals and unhandled exceptions through one host setting:

```csharp
using DKNet.AspCore.Extensions.Responses;
using FluentResults;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddErrorResponses(o =>
{
    o.StatusCode = ctx => ctx.Errors.Any(e => e.Code == "business-refusal") ? 422 : null;
    o.Customize = (problem, ctx) => problem.Extensions["source"] = ctx.Source.ToString();
});
var app = builder.Build();
app.MapGet("/refused", () => Result.Fail(
    new Error("Refused").WithMetadata("Code", "business-refusal")).Response());
await app.RunAsync();
```

![Command, validation or unhandled failure enters the shared factory, applies status selection, then customization before writing ProblemDetails.](diagrams/error-shaping.svg)

### One error-response setting

The example returns 422 with `type: UnprocessableEntity`, `title: Error`, `errors` containing message/code/field entries, and `traceId` from the current activity or request identifier. The factory does not set `Detail`. Command codes come from string `Code` error metadata; validation codes come from `ErrorCode`, with the member name as `Field`.

`ErrorResponseContext` carries required `Source` (`Command`, `Validation`, `Unhandled`) and `Errors`, plus an exception only for unhandled failures. It carries no route or `HttpContext`. `ErrorItem` is `ErrorItem(string Message, string? Code = null, string? Field = null)`.

Registration supplies the validation result factory, problem-details services, `ErrorResponseExceptionHandler` and startup middleware calling `UseExceptionHandler`. It does not add validation filters to every route; attach the host's validation filter where required. The first registered options singleton wins. A configure delegate is still invoked on each call before `TryAddSingleton` decides whether to retain it.

`ProducesCommons()` adds an endpoint exception filter to mapped routes, including in Development. It declines cancellation, an aborted request or a response already started. The public `ErrorResponseExceptionHandler` implements `IExceptionHandler`; its protected virtual `CreateProblemDetails` is the customization point when deriving a handler. Its `TryHandleAsync` returns handled without writing if the response started or request was aborted.

Without registration, `Response` failures still have the standard command-error body; FluentValidation uses its own default factory and there is no host-wide exception handler from this package. A route with `ProducesCommons` still has its endpoint exception filter.

## ⚙️ Configuration reference

| `ErrorResponseOptions` option | Type | Default | Effect |
|---|---|---|---|
| `StatusCode` | `Func<ErrorResponseContext, int?>?` | null | Select final status; null retains 400, NotFoundError's 404, or unhandled 500. |
| `Customize` | `Action<ProblemDetails, ErrorResponseContext>?` | null | Runs after status selection for every failure kind. |
| `UnhandledError` | `Func<ErrorResponseContext, ProblemDetails?>?` | null | Supplies an unhandled-exception body, or returns null for the default. |

`StatusCode` overrides the body callback's status when non-null. The shared finalizer updates `type` when that callback selects a status, then calls `Customize`.

## ⚠️ Gotchas & limits

- The default unhandled body exposes the exception message only in Development. Elsewhere it uses a fixed message and trace ID; never put secret exception details into custom output.
- Raw endpoints without `ProducesCommons` can still hit the developer exception page in Development. Production uses the registered host handler.
- `Customize` is invoked for all failure kinds; its own conditions decide which members appear. Host callbacks can alter the standard shape.
- A custom `UnhandledError` body is responsible for its own title, type and errors. The finalizer always adds trace ID; it does not rebuild omitted members.
- Antiforgery refusals use a separate fixed 400 factory invocation without these options.
- `MapGetList` refusals for invalid filters, search, sorting or date bounds return a bare `Results.Problem(error, 400)`. They also bypass `ErrorResponseOptions`.
