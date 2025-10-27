using Microsoft.AspNetCore.Diagnostics;

namespace SlimBus.Api.Configs.GlobalExceptions;

internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    #region Methods

    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, exception.Message);

        if (exception.InnerException is not null)
            exception = exception.InnerException;

        var problem = new ProblemDetails
        {
            Status = (int)HttpStatusCode.InternalServerError,
            Title = "Something went wrong!.",
            Detail = exception.Message,
            Type = exception.GetType().Name
        };

        return problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            { HttpContext = httpContext, ProblemDetails = problem, Exception = exception });
    }

    #endregion
}