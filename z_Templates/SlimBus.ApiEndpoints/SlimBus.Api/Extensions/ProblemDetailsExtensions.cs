namespace SlimBus.Api.Extensions;

[ExcludeFromCodeCoverage]
internal static class ProblemDetailsExtensions
{
    public static ProblemDetails? ToProblemDetails(this IResultBase result, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
    {
        if (result.IsSuccess) return null;

        var errors = result.Errors.Select(e => e.Message).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var firstMessage = errors.FirstOrDefault() ?? statusCode.ToString();

        return CreateProblemDetails(statusCode, firstMessage, errors);
    }

    public static ProblemDetails? ToProblemDetails(this ModelStateDictionary status)
    {
        if (status.IsValid) return null;

        var errors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in status)
        foreach (var err in value.Errors)
            errors.Add(err.ErrorMessage);

        var firstMessage = errors.FirstOrDefault() ?? nameof(HttpStatusCode.BadRequest);
        
        return CreateProblemDetails(HttpStatusCode.BadRequest, firstMessage, errors);
    }

    private static ProblemDetails CreateProblemDetails(HttpStatusCode statusCode, string detail, IEnumerable<string> errors)
    {
        return new ProblemDetails
        {
            Status = (int)statusCode,
            Type = statusCode.ToString(),
            Title = "Error",
            Detail = detail,
            Extensions =
            {
                ["errors"] = errors
            },
        };
    }
}