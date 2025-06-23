using Microsoft.AspNetCore.Mvc;

namespace SlimBus.App.Tests.Extensions;

public static class JsonExtensions
{
    private static readonly JsonSerializerOptions? Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<(bool success, TValue? result, ProblemDetails? error, string content)> As<TValue>(
        this HttpResponseMessage message) where TValue : class
    {
        var success = message.IsSuccessStatusCode;
        TValue? result = null;
        ProblemDetails? error = null;

        var str = await message.Content.ReadAsStringAsync();

        if (!string.IsNullOrEmpty(str))
        {
            if (success)
                result = JsonSerializer.Deserialize<TValue>(str, Options);
            else error = JsonSerializer.Deserialize<ProblemDetails>(str, Options);
        }

        return (success, result, error, str);
    }
}