using FluentResults;
using Microsoft.AspNetCore.Http;

namespace DKNet.AspCore.SlimBus;

public static class ResultResponseExtensions
{
    public static IResult Response<TObject>(this IResult<TObject> result, bool isCreated = false) =>
        !result.IsSuccess
            ? TypedResults.Problem(result.ToProblemDetails()!)
            : isCreated
                ? TypedResults.Created("/", result.Value)
                : result.ValueOrDefault is null
                    ? TypedResults.Ok()
                    : TypedResults.Json(result.Value);

    public static IResult Response(this IResultBase result, bool isCreated = false) =>
        result.IsSuccess
            ? isCreated ? TypedResults.Created() : TypedResults.Ok()
            : TypedResults.Problem(result.ToProblemDetails()!);
}