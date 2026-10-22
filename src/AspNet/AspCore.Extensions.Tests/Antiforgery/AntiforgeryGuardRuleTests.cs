// DRK-2373 Build tests — the antiforgery guard's rule edges (DRK-2362 §6/§6a rows the acceptance tests do not
// reach): the bearer-header shapes (R3), methods never checked (R2), a token only in a form field (R5), forged
// cookies (R5), form-bound endpoints the framework would also check (R7), custom secret cookie name, and a host
// that never registered the guard.

using System.Diagnostics;
using System.Net;
using System.Text.Json;
using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions.Antiforgery;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace AspCore.Extensions.Tests.Antiforgery;

public class AntiforgeryGuardRuleTests
{
    #region Methods

    private const string UploadPath = "/v1/upload";
    private const string FormPath = "/v1/form";
    private const string ProbePath = "/v1/probe";
    private const string VerdictPath = "/v1/verdict";

    [Fact]
    public void AntiforgeryGuardOptions_New_HasSpecDefaults()
    {
        var options = new AntiforgeryGuardOptions();

        options.TokenCookieName.ShouldBe("XSRF-TOKEN");
        options.HeaderName.ShouldBe("X-XSRF-TOKEN");
        options.SecretCookieName.ShouldBeNull();
    }

    // R3 / §6a D1–D2: only a single "Bearer <token>" value exempts a state-changing request.
    [Theory]
    [InlineData(200, "Bearer abc")]
    [InlineData(200, "bearer abc")]
    [InlineData(400, "Bearer")]
    [InlineData(400, "Bearer ")]
    [InlineData(400, "Basic dXNlcjpwYXNz")]
    [InlineData(400)]
    [InlineData(400, "Bearer abc", "Bearer def")]
    public async Task UseAntiforgeryConfig_PostWithAuthorizationShape_ExemptOnlyForOneNonBlankBearer(
        int expected, params string[] authorization)
    {
        await using var host = await StartAsync();

        var context = await SendHttpsAsync(host, HttpMethods.Post, HostingTestHost.AccountsPath,
            r => r.Headers.Authorization = authorization);

        context.Response.StatusCode.ShouldBe(expected);
    }

    // R2 / §6a D3: methods outside POST/PUT/PATCH/DELETE pass unchecked and get no token cookie.
    [Theory]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task UseAntiforgeryConfig_UncheckedMethodWithoutToken_PassesWithoutTokenCookie(string method)
    {
        await using var host = await StartAsync();

        var context = await SendHttpsAsync(host, method, ProbePath);

        context.Response.StatusCode.ShouldBe(200);
        context.Response.Headers.SetCookie.Count.ShouldBe(0);
    }

    // R2: the method match ignores case, so a lowercase "post" is still checked.
    [Fact]
    public async Task UseAntiforgeryConfig_LowercasePostWithoutToken_Refused()
    {
        await using var host = await StartAsync();

        var context = await SendHttpsAsync(host, "post", HostingTestHost.AccountsPath);

        context.Response.StatusCode.ShouldBe(400);
    }

    // R5 / §6a D4: the framework would accept the form field; the guard requires the header.
    [Fact]
    public async Task UseAntiforgeryConfig_TokenOnlyInFormField_Refused()
    {
        await using var host = await StartAsync();
        using var client = host.CreateClient("https://api.example.com");
        var session = await Session.StartAsync(client);

        var status = await session.SendAsync(client, HttpMethod.Post, HostingTestHost.AccountsPath,
            content: new FormUrlEncodedContent([new("__RequestVerificationToken", session.Token)]));

        status.ShouldBe(HttpStatusCode.BadRequest);
    }

    // R5 / §6a D5: a header token is only good with the secret cookie it was issued for.
    [Theory]
    [InlineData("altered secret cookie")]
    [InlineData("no cookies")]
    public async Task UseAntiforgeryConfig_HeaderTokenWithoutItsSecretCookie_Refused(string cookies)
    {
        await using var host = await StartAsync();
        using var client = host.CreateClient("https://api.example.com");
        var session = await Session.StartAsync(client);
        var forged = cookies == "no cookies"
            ? session with { Cookies = null }
            : session with { Cookies = $"{session.SecretName}=CfDJ8AlteredByHand; XSRF-TOKEN={session.Token}" };

        var status = await forged.SendAsync(client, HttpMethod.Post, HostingTestHost.AccountsPath, session.Token);

        status.ShouldBe(HttpStatusCode.BadRequest);
    }

    // R7 / §6a D7: a request the guard exempts or accepts is never refused by the framework's own check on a
    // form-bound endpoint, and the framework never answers 500 for a missing antiforgery middleware.
    [Theory]
    [InlineData(UploadPath, "bearer", HttpStatusCode.OK, "logo.png")]
    [InlineData(UploadPath, "header token", HttpStatusCode.OK, "logo.png")]
    [InlineData(FormPath, "bearer", HttpStatusCode.OK, "1000")]
    [InlineData(FormPath, "header token", HttpStatusCode.OK, "1000")]
    [InlineData(UploadPath, "cookies only", HttpStatusCode.BadRequest, null)]
    public async Task UseAntiforgeryConfig_FormBoundEndpoint_FrameworkCheckFollowsGuard(
        string path, string credential, HttpStatusCode expected, string? body)
    {
        await using var host = await StartAsync();
        using var client = host.CreateClient("https://api.example.com");
        var session = await Session.StartAsync(client);
        HttpContent content = path == UploadPath
            ? new MultipartFormDataContent { { new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]), "file", "logo.png" } }
            : new FormUrlEncodedContent([new("amount", "1000")]);

        using var response = await session.SendForResponseAsync(client, HttpMethod.Post, path,
            headerToken: credential == "header token" ? session.Token : null,
            authorization: credential == "bearer" ? "Bearer treasury-ops" : null,
            content: content);

        response.StatusCode.ShouldBe(expected);
        if (body is not null)
            (await response.Content.ReadAsStringAsync()).ShouldBe(body);
        else
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    // R4: a request no endpoint matches is still checked.
    [Fact]
    public async Task UseAntiforgeryConfig_PostToUnmappedPathWithoutToken_Refused()
    {
        await using var host = await StartAsync();

        var context = await SendHttpsAsync(host, HttpMethods.Post, "/v1/unmapped");

        context.Response.StatusCode.ShouldBe(400);
    }

    // R7: what the guard lets through reaches the endpoint with a valid antiforgery verdict and no error.
    [Fact]
    public async Task UseAntiforgeryConfig_BearerPostWithoutToken_EndpointSeesValidVerdict()
    {
        await using var host = await StartAsync();

        var context = await SendHttpsAsync(host, HttpMethods.Post, VerdictPath,
            r => r.Headers.Authorization = "Bearer treasury-ops");

        (await HostingTestHost.ReadBodyAsync(context)).ShouldBe("valid, no error");
    }

    // The refusal's traceId follows DKNet's convention: the current activity's id, else the request's identifier.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UseAntiforgeryConfig_RefusedPost_TraceIdIsActivityIdElseTraceIdentifier(bool noActivity)
    {
        await using var host = await StartAsync(clearActivity: noActivity);

        var context = await SendHttpsAsync(host, HttpMethods.Post, HostingTestHost.AccountsPath);

        var traceId = JsonDocument.Parse(await HostingTestHost.ReadBodyAsync(context)).RootElement
            .GetProperty("traceId").GetString()!;
        if (noActivity)
            traceId.ShouldBe(context.TraceIdentifier);
        else
            traceId.ShouldMatch("^00-[0-9a-f]{32}-[0-9a-f]{16}-0[01]$");
    }

    [Fact]
    public async Task UseAntiforgeryConfig_HostSetsSecretCookieName_SecretCookieUsesItAndTokenCookieIsSiteWide()
    {
        await using var host = await StartAsync(o => o.SecretCookieName = "__Host-af");
        using var client = host.CreateClient("https://api.example.com");

        var session = await Session.StartAsync(client);

        session.SecretName.ShouldBe("__Host-af");
        session.TokenCookiePath.ShouldBe("/");
    }

    [Fact]
    public async Task UseAntiforgeryConfig_GuardAdded_LogsOneEnabledLine()
    {
        await using var host = await StartAsync();

        host.Logs.Entries.Count(e => e.Message == "AntiforgeryConfig enabled").ShouldBe(1);
    }

    [Fact]
    public async Task UseAntiforgeryConfig_GuardNotAdded_PostWithoutTokenPassesAndNothingLogged()
    {
        await using var host = await StartAsync(addGuard: false);

        var context = await SendHttpsAsync(host, HttpMethods.Post, HostingTestHost.AccountsPath);

        context.Response.StatusCode.ShouldBe(200);
        host.Logs.Entries.ShouldNotContain(e => e.Message == "AntiforgeryConfig enabled");
    }

    private static Task<HttpContext> SendHttpsAsync(
        HostingTestHost host, string method, string path, Action<HttpRequest>? configure = null) =>
        host.SendAsync(method, path, c =>
        {
            c.Request.Scheme = "https";
            configure?.Invoke(c.Request);
        });

    private static Task<HostingTestHost> StartAsync(
        Action<AntiforgeryGuardOptions>? configure = null, bool addGuard = true, bool clearActivity = false) =>
        HostingTestHost.StartAsync(
            b =>
            {
                if (addGuard) b.Services.AddAntiforgeryConfig(configure);
            },
            app =>
            {
                if (clearActivity)
                    app.Use((context, next) =>
                    {
                        Activity.Current = null;
                        return next(context);
                    });
                app.UseRouting();
                app.UseAntiforgeryConfig();
                app.MapPost(UploadPath, (IFormFile file) => file.FileName);
                app.MapPost(FormPath, ([FromForm] string amount) => amount);
                app.MapPost(VerdictPath, (HttpContext c) =>
                    c.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: true, Error: null }
                        ? "valid, no error"
                        : "not valid");
                app.MapMethods(ProbePath, [HttpMethods.Head, HttpMethods.Options], () => Results.Ok());
            });

    #endregion

    /// <summary>One browser session: the cookies and request token the first GET issued.</summary>
    private sealed record Session(string? Cookies, string Token, string SecretName, string? TokenCookiePath)
    {
        public static async Task<Session> StartAsync(HttpClient client)
        {
            using var response = await client.GetAsync(HostingTestHost.AccountsPath);
            var setCookies = SetCookieHeaderValue.ParseList(response.Headers.GetValues("Set-Cookie").ToList());
            var token = setCookies.Single(c => c.Name == "XSRF-TOKEN");
            return new Session(
                string.Join("; ", setCookies.Select(c => $"{c.Name}={c.Value}")),
                token.Value.ToString(),
                setCookies.Single(c => c.Name != "XSRF-TOKEN").Name.ToString(),
                token.Path.ToString());
        }

        public async Task<HttpStatusCode> SendAsync(
            HttpClient client, HttpMethod method, string path, string? headerToken = null, HttpContent? content = null)
        {
            using var response = await SendForResponseAsync(client, method, path, headerToken, content: content);
            return response.StatusCode;
        }

        public Task<HttpResponseMessage> SendForResponseAsync(
            HttpClient client, HttpMethod method, string path, string? headerToken = null,
            string? authorization = null, HttpContent? content = null)
        {
            var request = new HttpRequestMessage(method, path) { Content = content };
            if (Cookies is not null) request.Headers.Add("Cookie", Cookies);
            if (headerToken is not null) request.Headers.Add("X-XSRF-TOKEN", headerToken);
            if (authorization is not null) request.Headers.TryAddWithoutValidation("Authorization", authorization);
            return client.SendAsync(request);
        }
    }
}
