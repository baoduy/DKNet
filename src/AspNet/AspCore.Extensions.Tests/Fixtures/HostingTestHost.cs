using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AspCore.Extensions.Tests.Fixtures;

/// <summary>
///     One real minimal-API host for DRK-2362's shared hosting scenarios (rate limiting, request bounds, HTTPS,
///     forwarded headers, CORS, antiforgery, sensitive-data accessor). Each test supplies the settings, the service
///     registrations and the pipeline steps under test; the host maps <c>/v1/accounts</c> for every verb (reading
///     the whole request body, answering 200), records
///     every log entry in <see cref="Logs" />, and runs on TestServer or, where TestServer cannot show the
///     behaviour (Kestrel limits, the <c>Server</c> header), on real Kestrel at a loopback port.
/// </summary>
public sealed class HostingTestHost : IAsyncDisposable
{
    #region Fields

    public const string AccountsPath = "/v1/accounts";

    private readonly WebApplication _app;

    #endregion

    #region Constructors

    private HostingTestHost(WebApplication app, CapturingLoggerProvider logs)
    {
        _app = app;
        Logs = logs;
    }

    #endregion

    #region Properties

    /// <summary>Every log entry the host wrote, from build to dispose.</summary>
    public CapturingLoggerProvider Logs { get; }

    /// <summary>The TestServer the host runs on. Only for a host started with <c>useKestrel: false</c>.</summary>
    public TestServer Server => _app.GetTestServer();

    /// <summary>The loopback address Kestrel listens on. Only for a host started with <c>useKestrel: true</c>.</summary>
    public Uri KestrelAddress => new(_app.Urls.First());

    #endregion

    #region Methods

    /// <summary>
    ///     Builds and starts one host.
    /// </summary>
    /// <param name="configureServices">Adds settings and registers services on the builder.</param>
    /// <param name="configurePipeline">Adds the pipeline steps under test, and any extra routes.</param>
    /// <param name="configureAccountsRoute">Adds endpoint metadata (authorization, antiforgery) to <c>/v1/accounts</c>.</param>
    /// <param name="useKestrel">Runs on real Kestrel at <c>127.0.0.1:0</c> instead of TestServer.</param>
    public static async Task<HostingTestHost> StartAsync(
        Action<WebApplicationBuilder>? configureServices = null,
        Action<WebApplication>? configurePipeline = null,
        Action<RouteHandlerBuilder>? configureAccountsRoute = null,
        bool useKestrel = false)
    {
        var logs = new CapturingLoggerProvider();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        if (useKestrel)
            builder.WebHost.UseUrls("http://127.0.0.1:0");
        else
            builder.WebHost.UseTestServer();

        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.Logging.SetMinimumLevel(LogLevel.Trace);

        configureServices?.Invoke(builder);

        var app = builder.Build();
        try
        {
            configurePipeline?.Invoke(app);

            var accounts = app.MapMethods(
                AccountsPath,
                [HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete],
                async (HttpRequest request) =>
                {
                    // Reads the body, so Kestrel's body-size limit is enforced on it.
                    await request.Body.CopyToAsync(Stream.Null);
                    return Results.Ok("ok");
                });
            configureAccountsRoute?.Invoke(accounts);

            await app.StartAsync();
            return new HostingTestHost(app, logs);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    /// <summary>Settings from an in-memory dictionary.</summary>
    public static void AddSettings(WebApplicationBuilder builder, params (string Key, string? Value)[] settings) =>
        builder.Configuration.AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)));

    /// <summary>Settings from JSON — the only source that can express an empty array (<c>[]</c>).</summary>
    public static void AddJsonSettings(WebApplicationBuilder builder, string json) =>
        builder.Configuration.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    /// <summary>A TestServer client whose requests go to <paramref name="baseAddress" />.</summary>
    public HttpClient CreateClient(string baseAddress = "http://localhost")
    {
        var client = Server.CreateClient();
        client.BaseAddress = new Uri(baseAddress);
        return client;
    }

    /// <summary>
    ///     Sends one request through TestServer with full control of the connection — the only way to set the
    ///     peer address (<see cref="ConnectionInfo.RemoteIpAddress" />), which TestServer otherwise leaves null.
    /// </summary>
    public Task<HttpContext> SendAsync(string method, string path, Action<HttpContext>? configure = null) =>
        Server.SendAsync(ctx =>
        {
            ctx.Request.Method = method;
            ctx.Request.Path = path;
            configure?.Invoke(ctx);
        });

    /// <summary>The response body of a request sent with <see cref="SendAsync" />.</summary>
    public static async Task<string> ReadBodyAsync(HttpContext context)
    {
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    #endregion
}

/// <summary>
///     A test stand-in for bearer authentication, decided per request: <c>Authorization: Bearer &lt;user&gt;</c>
///     signs in <c>&lt;user&gt;</c> with the comma-separated roles of the <c>X-Test-Roles</c> header;
///     <c>Bearer expired</c> fails authentication; any other <c>Authorization</c> value, or none, signs no one in.
/// </summary>
public sealed class BearerTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    #region Fields

    public const string ExpiredToken = "expired";
    public const string RolesHeader = "X-Test-Roles";
    public const string SchemeName = "Bearer";

    #endregion

    #region Methods

    /// <summary>Registers this handler as the default scheme, plus authorization.</summary>
    public static void Register(IServiceCollection services)
    {
        services.AddAuthentication(SchemeName)
            .AddScheme<AuthenticationSchemeOptions, BearerTestAuthHandler>(SchemeName, null);
        services.AddAuthorization();
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(SchemeName + " ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var token = header[(SchemeName.Length + 1)..].Trim();
        if (token == ExpiredToken)
            return Task.FromResult(AuthenticateResult.Fail("The bearer token has expired."));

        var claims = new List<Claim> { new(ClaimTypes.Name, token) };
        claims.AddRange(Request.Headers[RolesHeader].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(role => new Claim(ClaimTypes.Role, role)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }

    #endregion
}
