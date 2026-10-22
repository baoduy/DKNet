// DRK-2362 §5 acceptance tests — CORS (DRK-2374). Every scenario fails today: CorsConfig is a stub @ the
// acceptance-tests commit, so AddCorsConfig/UseCorsConfig throw NotImplementedException. Settings load from JSON:
// only JSON can express the empty lists ("no methods", "no headers") the outline names.

using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions.Hosting;

namespace AspCore.Extensions.Tests.Hosting;

public class CorsConfigTests
{
    #region Methods

    // Scenario Outline: CORS answers only allowed origins, methods and headers
    [Theory]
    [InlineData("no origin", "default methods", "default headers", "https://app.example.com", "GET", "Accept", "refused")]
    [InlineData("https://app.example.com", "default methods", "default headers", "https://evil.example", "GET", "Accept", "refused")]
    [InlineData("https://app.example.com", "default methods", "default headers", "https://app.example.com", "GET", "Accept", "allowed")]
    [InlineData("https://app.example.com", "default methods", "default headers", "https://app.example.com", "POST", "Content-Type", "allowed")]
    [InlineData("https://app.example.com", "default methods", "default headers", "https://app.example.com", "PUT", "Authorization", "allowed")]
    [InlineData("https://app.example.com", "default methods", "default headers", "https://app.example.com", "PATCH", "X-Idempotency-Key", "allowed")]
    [InlineData("https://app.example.com", "default methods", "default headers", "https://app.example.com", "DELETE", "Accept", "refused")]
    [InlineData("https://app.example.com", "GET only", "default headers", "https://app.example.com", "POST", "Accept", "refused")]
    [InlineData("https://app.example.com", "no methods", "default headers", "https://app.example.com", "GET", "Accept", "refused")]
    [InlineData("https://app.example.com", "default methods", "Authorization and Idempotency-Key", "https://app.example.com", "POST", "Idempotency-Key", "allowed")]
    [InlineData("https://app.example.com", "default methods", "Authorization and Idempotency-Key", "https://app.example.com", "POST", "X-Idempotency-Key", "refused")]
    [InlineData("https://app.example.com", "default methods", "no headers", "https://app.example.com", "POST", "Content-Type", "refused")]
    public async Task UseCorsConfig_PreflightFromOriginForMethodAndHeader_AnsweredAsExpected(
        string origins, string methods, string headers, string origin, string method, string header, string result)
    {
        await using var host = await StartCorsHostAsync(CorsJson(origins, methods, headers));
        using var client = host.CreateClient();
        using var preflight = new HttpRequestMessage(HttpMethod.Options, HostingTestHost.AccountsPath);
        preflight.Headers.Add("Origin", origin);
        preflight.Headers.Add("Access-Control-Request-Method", method);
        preflight.Headers.Add("Access-Control-Request-Headers", header);

        using var response = await client.SendAsync(preflight);

        (IsAllowed(response, origin, method, header) ? "allowed" : "refused").ShouldBe(result);
    }

    // Scenario: CORS never allows credentials
    [Fact]
    public async Task UseCorsConfig_AllowedOriginSendsGetWithCredentials_AnswerDoesNotAllowCredentials()
    {
        await using var host = await StartCorsHostAsync("""{ "Cors": { "AllowedOrigins": ["https://app.example.com"] } }""");
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, HostingTestHost.AccountsPath);
        request.Headers.Add("Origin", "https://app.example.com");
        request.Headers.Add("Cookie", "session=treasury-ops");

        using var response = await client.SendAsync(request);

        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(["https://app.example.com"]);
        response.Headers.Contains("Access-Control-Allow-Credentials").ShouldBeFalse();
    }

    private static Task<HostingTestHost> StartCorsHostAsync(string json) =>
        HostingTestHost.StartAsync(
            b =>
            {
                HostingTestHost.AddJsonSettings(b, json);
                b.Services.AddCorsConfig(b.Configuration);
            },
            app => app.UseCorsConfig());

    /// <summary>The <c>Cors</c> settings for one outline row; a "default" column leaves its key out.</summary>
    private static string CorsJson(string origins, string methods, string headers)
    {
        var members = new List<string>();
        if (origins != "no origin")
            members.Add($"\"AllowedOrigins\": [\"{origins}\"]");

        switch (methods)
        {
            case "GET only":
                members.Add("\"AllowedMethods\": [\"GET\"]");
                break;
            case "no methods":
                members.Add("\"AllowedMethods\": []");
                break;
        }

        switch (headers)
        {
            case "Authorization and Idempotency-Key":
                members.Add("\"AllowedHeaders\": [\"Authorization\", \"Idempotency-Key\"]");
                break;
            case "no headers":
                members.Add("\"AllowedHeaders\": []");
                break;
        }

        return $"{{ \"Cors\": {{ {string.Join(", ", members)} }} }}";
    }

    /// <summary>
    ///     A browser's reading of a preflight answer: the request may go ahead only when the answer allows the
    ///     origin AND lists the method AND lists the header.
    /// </summary>
    private static bool IsAllowed(HttpResponseMessage response, string origin, string method, string header) =>
        Values(response, "Access-Control-Allow-Origin").Contains(origin, StringComparer.Ordinal)
        && Values(response, "Access-Control-Allow-Methods").Contains(method, StringComparer.OrdinalIgnoreCase)
        && Values(response, "Access-Control-Allow-Headers").Contains(header, StringComparer.OrdinalIgnoreCase);

    private static string[] Values(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values)
            ? values.SelectMany(v => v.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).ToArray()
            : [];

    #endregion
}
