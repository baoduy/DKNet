namespace SlimBus.Api.Extensions;

[ExcludeFromCodeCoverage]
internal static class ProblemDetailsExtensions
{
    public static ProblemDetails? ToProblemDetails(this IResultBase result, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
    {
        if (result.IsSuccess) return null;

        var errors = result.Errors.Select(e => e.Message).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var firstMessage = errors.FirstOrDefault() ?? statusCode.ToString();

        var problem = new ProblemDetails
        {
            Status = (int)statusCode,
            Type = statusCode.ToString(),
            Title = "Error",
            Detail = firstMessage,
            Extensions =
            {
                ["errors"] = errors
            },
        };

        return problem;
    }

    public static ProblemDetails? ToProblemDetails(this ModelStateDictionary status)
    {
        if (status.IsValid) return null;

        var errors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in status)
        foreach (var err in value.Errors)
            errors.Add(err.ErrorMessage);

        var firstMessage = errors.FirstOrDefault() ?? nameof(HttpStatusCode.BadRequest);
        var problem = new ProblemDetails
        {
            Status = (int)HttpStatusCode.BadRequest,
            Title = "Error",
            Detail = firstMessage,
            Type = nameof(HttpStatusCode.BadRequest),
            Extensions =
            {
                ["errors"] = errors
            },
        };

        return problem;
    }
}