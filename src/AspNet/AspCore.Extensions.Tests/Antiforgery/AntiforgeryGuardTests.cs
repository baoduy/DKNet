// DRK-2362 §5 acceptance tests — the fixed antiforgery guard (DRK-2373). Every scenario fails today:
// AntiforgeryConfig is a stub @ the acceptance-tests commit, so AddAntiforgeryConfig/UseAntiforgeryConfig throw
// NotImplementedException. Requests go to https:// so Secure cookies round-trip; TestServer keeps no cookie jar,
// so Browser below carries cookies by hand. "/v1/accounts" needs a signed-in user only when a bearer header is
// sent, so the expired-bearer row gets its 401 from authentication while every other row reaches the guard.

using System.Net;
using System.Text;
using System.Text.Json;
using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using SameSiteMode = Microsoft.Net.Http.Headers.SameSiteMode;

namespace AspCore.Extensions.Tests.Antiforgery;

public class AntiforgeryGuardTests
{
    #region Methods

    private const string TokenCookie = "XSRF-TOKEN";
    private const string TokenHeader = "X-XSRF-TOKEN";

    // Scenario: A browser's first GET gives it a secret cookie and a token
    [Fact]
    public async Task UseAntiforgeryConfig_FirstGetWithNoCookies_SetsHttpOnlySecretAndReadableTokenCookie()
    {
        await using var host = await StartAntiforgeryHostAsync();
        var browser = new Browser(host);

        var setCookies = await browser.GetAsync();

        setCookies.Count.ShouldBe(2);
        var secret = setCookies.Single(c => c.Name.Value != TokenCookie);
        secret.HttpOnly.ShouldBeTrue();
        secret.Secure.ShouldBeTrue();
        secret.SameSite.ShouldBe(SameSiteMode.Strict);

        var token = setCookies.Single(c => c.Name.Value == TokenCookie);
        token.HttpOnly.ShouldBeFalse();
        token.Secure.ShouldBeTrue();
        token.SameSite.ShouldBe(SameSiteMode.Strict);
    }

    // Scenario: A browser with an invalid secret cookie gets a new one
    [Fact]
    public async Task UseAntiforgeryConfig_GetWithAlteredSecretCookie_SetsNewSecretAndTokenCookie()
    {
        await using var host = await StartAntiforgeryHostAsync();
        var browser = new Browser(host);
        await browser.GetAsync();
        var secretName = browser.SecretCookieName;
        browser.Cookies[secretName] = "CfDJ8AlteredByHand";

        var setCookies = await browser.GetAsync();

        setCookies.Single(c => c.Name.Value == secretName).Value.Value.ShouldNotBe("CfDJ8AlteredByHand");
        setCookies.Count(c => c.Name.Value == TokenCookie).ShouldBe(1);
        (await browser.SendAsync(HttpMethod.Post, headerToken: browser.Cookies[TokenCookie])).ShouldBe(HttpStatusCode.OK);
    }

    // Scenario: Parallel GET requests keep each other's tokens valid
    [Fact]
    public async Task UseAntiforgeryConfig_PostWithTokenFromEarlierOfTwoGets_Accepted()
    {
        await using var host = await StartAntiforgeryHostAsync();
        var browser = new Browser(host);
        var firstGet = await browser.GetAsync();
        var tokenA = browser.Cookies[TokenCookie];
        var secretName = browser.SecretCookieName;
        firstGet.Count(c => c.Name.Value == secretName).ShouldBe(1);

        var secondGet = await browser.GetAsync();

        (await browser.SendAsync(HttpMethod.Post, headerToken: tokenA)).ShouldBe(HttpStatusCode.OK);
        secondGet.Count(c => c.Name.Value == secretName).ShouldBe(0);
        secondGet.Count(c => c.Name.Value == TokenCookie).ShouldBe(1);
    }

    // Scenario Outline: Antiforgery checks state-changing requests without a bearer token
    [Theory]
    [InlineData("POST", "with its token in the \"X-XSRF-TOKEN\" header", "accepted")]
    [InlineData("PUT", "with its token in the \"X-XSRF-TOKEN\" header", "accepted")]
    [InlineData("PATCH", "with its token in the \"X-XSRF-TOKEN\" header", "accepted")]
    [InlineData("DELETE", "with its token in the \"X-XSRF-TOKEN\" header", "accepted")]
    [InlineData("POST", "with its cookies but no header token", "refused with 400")]
    [InlineData("PUT", "with its cookies but no header token", "refused with 400")]
    [InlineData("PATCH", "with its cookies but no header token", "refused with 400")]
    [InlineData("DELETE", "with its cookies but no header token", "refused with 400")]
    [InlineData("POST", "with a header token from another browser session", "refused with 400")]
    [InlineData("POST", "with a bearer token and no header token", "accepted")]
    [InlineData("POST", "with a basic sign-in header and no header token", "refused with 400")]
    [InlineData("POST", "with an expired bearer token and no header token", "gets 401 from authentication, not 400 from the token check")]
    [InlineData("POST", "with no cookies, no bearer token and no header token", "refused with 400")]
    [InlineData("GET", "with no header token", "accepted")]
    public async Task UseAntiforgeryConfig_StateChangingRequest_CheckedUnlessBearer(string method, string with, string result)
    {
        await using var host = await StartAntiforgeryHostAsync();
        var browser = new Browser(host);
        await browser.GetAsync();
        var ownToken = browser.Cookies[TokenCookie];

        var status = with switch
        {
            "with its token in the \"X-XSRF-TOKEN\" header" =>
                await browser.SendAsync(new HttpMethod(method), headerToken: ownToken),
            "with its cookies but no header token" or "with no header token" =>
                await browser.SendAsync(new HttpMethod(method)),
            "with a header token from another browser session" =>
                await browser.SendAsync(new HttpMethod(method), headerToken: await AnotherSessionTokenAsync(host)),
            "with a bearer token and no header token" =>
                await browser.SendAsync(new HttpMethod(method), authorization: "Bearer treasury-ops"),
            "with a basic sign-in header and no header token" =>
                await browser.SendAsync(new HttpMethod(method), authorization: "Basic dXNlcjpwYXNz"),
            "with an expired bearer token and no header token" =>
                await browser.SendAsync(new HttpMethod(method), authorization: "Bearer " + BearerTestAuthHandler.ExpiredToken),
            "with no cookies, no bearer token and no header token" =>
                await new Browser(host).SendAsync(new HttpMethod(method)),
            _ => throw new ArgumentOutOfRangeException(nameof(with), with, "Not a row of the outline.")
        };

        status.ShouldBe(result switch
        {
            "accepted" => HttpStatusCode.OK,
            "refused with 400" => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.Unauthorized
        });
    }

    // Scenario: A forged form post gets DKNet's standard error body
    [Fact]
    public async Task UseAntiforgeryConfig_CrossSiteFormPostWithCookies_RefusedWithStandardProblemBody()
    {
        await using var host = await StartAntiforgeryHostAsync();
        var browser = new Browser(host);
        await browser.GetAsync();

        using var request = browser.CreateRequest(HttpMethod.Post);
        request.Headers.Add("Origin", "https://evil.example");
        request.Content = new FormUrlEncodedContent([new("amount", "1000"), new("to", "ACC-6666")]);
        using var response = await browser.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("title").GetString().ShouldBe("Error");
        body.GetProperty("status").GetInt32().ShouldBe(400);
        body.GetProperty("type").GetString().ShouldBe("BadRequest");
        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        errors.Count.ShouldBe(1);
        errors[0].GetProperty("message").GetString().ShouldBe("The antiforgery token is missing or invalid.");
        errors[0].GetProperty("code").GetString().ShouldBe("Antiforgery");
        body.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    // Scenario: A host sets its own token names
    [Fact]
    public async Task UseAntiforgeryConfig_HostNamesTokenCookieAndHeader_PostWithThoseNamesAccepted()
    {
        await using var host = await StartAntiforgeryHostAsync(o =>
        {
            o.TokenCookieName = "x-csrf-header";
            o.HeaderName = "x-csrf-header";
        });
        var browser = new Browser(host, tokenCookieName: "x-csrf-header");
        var setCookies = await browser.GetAsync();
        setCookies.Count(c => c.Name.Value == "x-csrf-header").ShouldBe(1);

        var status = await browser.SendAsync(
            HttpMethod.Post, headerToken: browser.Cookies["x-csrf-header"], headerName: "x-csrf-header");

        status.ShouldBe(HttpStatusCode.OK);
    }

    // Scenario: A route that turns the check off is not checked
    [Fact]
    public async Task UseAntiforgeryConfig_FileUploadToRouteThatDisablesCheck_Accepted()
    {
        await using var host = await StartAntiforgeryHostAsync();
        var browser = new Browser(host);
        await browser.GetAsync();

        using var request = browser.CreateRequest(HttpMethod.Post, "/v1/files");
        var file = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        request.Content = new MultipartFormDataContent { { file, "file", "logo.png" } };
        using var response = await browser.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("logo.png");
    }

    private static async Task<string> AnotherSessionTokenAsync(HostingTestHost host)
    {
        var other = new Browser(host);
        await other.GetAsync();
        return other.Cookies[TokenCookie];
    }

    private static Task<HostingTestHost> StartAntiforgeryHostAsync(Action<AntiforgeryGuardOptions>? configure = null) =>
        HostingTestHost.StartAsync(
            b =>
            {
                BearerTestAuthHandler.Register(b.Services);
                b.Services.AddAntiforgeryConfig(configure);
            },
            app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseAntiforgeryConfig();
                app.MapPost("/v1/files", (IFormFile file) => file.FileName).DisableAntiforgery();
            },
            route => route.RequireAuthorization(policy => policy.RequireAssertion(ctx =>
                ctx.User.Identity?.IsAuthenticated == true
                || ctx.Resource is not HttpContext http
                || !http.Request.Headers.Authorization.ToString()
                    .StartsWith(BearerTestAuthHandler.SchemeName + " ", StringComparison.OrdinalIgnoreCase))));

    #endregion

    /// <summary>A browser on https://api.example.com that keeps the cookies the host sets and sends them back.</summary>
    private sealed class Browser(HostingTestHost host, string tokenCookieName = TokenCookie)
    {
        public HttpClient Client { get; } = host.CreateClient("https://api.example.com");

        public Dictionary<string, string> Cookies { get; } = new(StringComparer.Ordinal);

        /// <summary>The name of the secret cookie: the one cookie held that is not the token cookie.</summary>
        public string SecretCookieName => Cookies.Keys.Single(k => k != tokenCookieName);

        public HttpRequestMessage CreateRequest(HttpMethod method, string path = HostingTestHost.AccountsPath)
        {
            var request = new HttpRequestMessage(method, path);
            if (Cookies.Count > 0)
                request.Headers.Add("Cookie", string.Join("; ", Cookies.Select(c => $"{c.Key}={c.Value}")));
            return request;
        }

        /// <summary>Sends GET "/v1/accounts", keeps the cookies it sets, and returns them.</summary>
        public async Task<IList<SetCookieHeaderValue>> GetAsync()
        {
            using var response = await Client.SendAsync(CreateRequest(HttpMethod.Get));
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var setCookies = response.Headers.TryGetValues("Set-Cookie", out var values)
                ? SetCookieHeaderValue.ParseList(values.ToList())
                : [];
            foreach (var cookie in setCookies)
                Cookies[cookie.Name.Value!] = cookie.Value.Value!;
            return setCookies;
        }

        /// <summary>Sends a request to "/v1/accounts" with the cookies held and the given headers.</summary>
        public async Task<HttpStatusCode> SendAsync(
            HttpMethod method, string? headerToken = null, string? authorization = null, string headerName = TokenHeader)
        {
            using var request = CreateRequest(method);
            if (headerToken is not null)
                request.Headers.Add(headerName, headerToken);
            if (authorization is not null)
                request.Headers.TryAddWithoutValidation("Authorization", authorization);
            if (method != HttpMethod.Get)
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            using var response = await Client.SendAsync(request);
            return response.StatusCode;
        }
    }
}
