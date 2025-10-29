using System.Net;
using FluentResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DKNet.AspCore.SlimBus;

public static class ProblemDetailsExtensions
{
    #region Methods

    private static ProblemDetails CreateProblemDetails(
        HttpStatusCode statusCode,
        string detail,
        IEnumerable<string> errors) =>
        new()
        {
            Status = (int)statusCode,
            Type = statusCode.ToString(),
            Title = "Error",
            Detail = detail,
            Extensions =
            {
                ["errors"] = errors
            }
        };

    public static ProblemDetails? ToProblemDetails(
        this IResultBase result,
        HttpStatusCode statusCode = HttpStatusCode.BadRequest)
    {
        if (result.IsSuccess)
        {
            return null;
        }

        var errors = result.Errors.Select(e => e.Message).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var firstMessage = errors.FirstOrDefault() ?? statusCode.ToString();

        return CreateProblemDetails(statusCode, firstMessage, errors);
    }

    public static ProblemDetails? ToProblemDetails(this ModelStateDictionary status)
    {
        if (status.IsValid)
        {
            return null;
        }

        var errors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, value) in status)
        foreach (var err in value.Errors)
        {
            errors.Add(err.ErrorMessage);
        }

        var firstMessage = errors.FirstOrDefault() ?? nameof(HttpStatusCode.BadRequest);

        return CreateProblemDetails(HttpStatusCode.BadRequest, firstMessage, errors);
    }

    #endregion
}