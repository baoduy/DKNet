// DRK-2362 §5 acceptance tests — rate limiting (DRK-2374). Every scenario fails today: RateLimitConfig is a
// stub @ the acceptance-tests commit, so AddRateLimitConfig/UseRateLimitConfig throw NotImplementedException.

using System.Net;
using System.Threading.RateLimiting;
using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions.Hosting;
using DKNet.AspCore.Extensions.RateLimits;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Extensions.Tests.RateLimits;

public class RateLimitConfigTests
{
    #region Methods

    private const string SignedInUser = "the signed-in user ";
    private const string AnonymousClientAt = "an anonymous client at ";

    // Scenario: Two hosts in one process keep their own setup
    [Fact]
    public async Task UseRateLimitConfig_SecondHostWithoutAddRateLimitConfig_AcceptsEveryRequest()
    {
        await using var ordersApi = await StartRateLimitedHostAsync(requestLimit: 1);
        await using var billingApi = await HostingTestHost.StartAsync(
            configurePipeline: app => app.UseRateLimitConfig());

        var billingFirst = await SendAsync(billingApi, Caller(AnonymousClientAt + "203.0.113.9"));
        var billingSecond = await SendAsync(billingApi, Caller(AnonymousClientAt + "203.0.113.9"));

        billingFirst.ShouldBe(StatusCodes.Status200OK);
        billingSecond.ShouldBe(StatusCodes.Status200OK);

        // orders-api, in the same process, still limits: its second request in the second is refused.
        (await SendAsync(ordersApi, Caller(AnonymousClientAt + "203.0.113.9"))).ShouldBe(StatusCodes.Status200OK);
        (await SendAsync(ordersApi, Caller(AnonymousClientAt + "203.0.113.9"))).ShouldBe(StatusCodes.Status429TooManyRequests);
    }

    // Scenario Outline: Rate limiting counts each caller in its own window
    [Theory]
    [InlineData("the signed-in user treasury-ops", "the signed-in user treasury-ops", 429)]
    [InlineData("the signed-in user treasury-ops", "the signed-in user payments-ops", 200)]
    [InlineData("an anonymous client at 203.0.113.9", "an anonymous client at 203.0.113.9", 429)]
    [InlineData("an anonymous client at 203.0.113.9", "an anonymous client at 203.0.113.10", 200)]
    public async Task UseRateLimitConfig_CallerAfterEarlierCallerUsedItsWindow_GetsExpectedStatus(
        string earlierCaller, string caller, int status)
    {
        await using var host = await StartRateLimitedHostAsync(requestLimit: 2);

        (await SendAsync(host, Caller(earlierCaller))).ShouldBe(StatusCodes.Status200OK);
        (await SendAsync(host, Caller(earlierCaller))).ShouldBe(StatusCodes.Status200OK);

        (await SendAsync(host, Caller(caller))).ShouldBe(status);
    }

    // Scenario: Rate limiting caps requests in flight per caller
    [Fact]
    public async Task UseRateLimitConfig_ThirdRequestWhileTwoAreInFlight_GetsTooManyRequests()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var entered = new SemaphoreSlim(0);

        // The window allows 100, so only the in-flight cap of 2 can refuse the third request.
        await using var host = await StartRateLimitedHostAsync(
            requestLimit: 100,
            concurrentLimit: 2,
            mapRoutes: app => app.MapGet("/v1/slow", async () =>
            {
                entered.Release();
                await release.Task;
                return Results.Ok();
            }));

        var treasuryOps = Caller(SignedInUser + "treasury-ops");
        var first = host.SendAsync(HttpMethods.Get, "/v1/slow", treasuryOps);
        var second = host.SendAsync(HttpMethods.Get, "/v1/slow", treasuryOps);
        HttpContext third;
        try
        {
            (await entered.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue("the first slow request never started");
            (await entered.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue("the second slow request never started");

            third = await host.SendAsync(HttpMethods.Get, "/v1/slow", treasuryOps);
        }
        finally
        {
            release.TrySetResult();
        }

        third.Response.StatusCode.ShouldBe(StatusCodes.Status429TooManyRequests);
        (await first).Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        (await second).Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    // Scenario: A forged forwarded address does not reset a caller's limit
    [Fact]
    public async Task UseRateLimitConfig_ThirdRequestClaimingAnotherForwardedAddress_GetsTooManyRequests()
    {
        await using var host = await HostingTestHost.StartAsync(
            b =>
            {
                AddRateLimitSettings(b, requestLimit: 2, concurrentLimit: 2);
                BearerTestAuthHandler.Register(b.Services);
                b.Services.AddForwardedHeadersConfig(b.Configuration);
                b.Services.AddRateLimitConfig(b.Configuration);
            },
            app =>
            {
                app.UseForwardedHeadersConfig();
                app.UseAuthentication();
                app.UseRateLimitConfig();
            });

        var client = Caller(AnonymousClientAt + "203.0.113.9");
        (await SendAsync(host, client)).ShouldBe(StatusCodes.Status200OK);
        (await SendAsync(host, client)).ShouldBe(StatusCodes.Status200OK);

        var third = await SendAsync(host, ctx =>
        {
            client(ctx);
            ctx.Request.Headers["X-Forwarded-For"] = "198.51.100.7";
        });

        third.ShouldBe(StatusCodes.Status429TooManyRequests);
    }

    // Scenario: Requests with no client address share one bucket
    [Fact]
    public async Task UseRateLimitConfig_ThirdRequestWithNoClientAddressForAnotherHostName_GetsTooManyRequests()
    {
        await using var host = await StartRateLimitedHostAsync(requestLimit: 2);

        (await SendAsync(host, NoClientAddressFor("a.example"))).ShouldBe(StatusCodes.Status200OK);
        (await SendAsync(host, NoClientAddressFor("b.example"))).ShouldBe(StatusCodes.Status200OK);

        (await SendAsync(host, NoClientAddressFor("c.example"))).ShouldBe(StatusCodes.Status429TooManyRequests);
    }

    // Scenario: The app supplies its own limit for a caller
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UseRateLimitConfig_AppGivesCallerFiveRequestsPerSecond_AcceptsAllFive(bool appRegistersFirst)
    {
        await using var host = await HostingTestHost.StartAsync(
            b =>
            {
                AddRateLimitSettings(b, requestLimit: 2, concurrentLimit: 2);
                BearerTestAuthHandler.Register(b.Services);
                if (appRegistersFirst)
                    b.Services.AddSingleton<IRateLimitOptionsProvider, TreasuryOpsLimitProvider>();
                b.Services.AddRateLimitConfig(b.Configuration);
                if (!appRegistersFirst)
                    b.Services.AddSingleton<IRateLimitOptionsProvider, TreasuryOpsLimitProvider>();
            },
            app =>
            {
                app.UseAuthentication();
                app.UseRateLimitConfig();
            });

        var statuses = new List<int>();
        for (var i = 0; i < 5; i++)
            statuses.Add(await SendAsync(host, Caller(SignedInUser + "treasury-ops")));

        statuses.ShouldBe([200, 200, 200, 200, 200]);
    }

    private static void AddRateLimitSettings(WebApplicationBuilder builder, int requestLimit, int concurrentLimit) =>
        HostingTestHost.AddSettings(
            builder,
            ("RateLimit:DefaultRequestLimit", requestLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("RateLimit:DefaultConcurrentLimit", concurrentLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("RateLimit:TimeWindowInSeconds", "1"));

    private static Task<HostingTestHost> StartRateLimitedHostAsync(
        int requestLimit,
        int concurrentLimit = 2,
        Action<WebApplication>? mapRoutes = null) =>
        HostingTestHost.StartAsync(
            b =>
            {
                AddRateLimitSettings(b, requestLimit, concurrentLimit);
                BearerTestAuthHandler.Register(b.Services);
                b.Services.AddRateLimitConfig(b.Configuration);
            },
            app =>
            {
                app.UseAuthentication();
                app.UseRateLimitConfig();
                mapRoutes?.Invoke(app);
            });

    /// <summary>A caller as the spec's examples name it: a signed-in user, or an anonymous client at an address.</summary>
    private static Action<HttpContext> Caller(string description)
    {
        if (description.StartsWith(SignedInUser, StringComparison.Ordinal))
        {
            var user = description[SignedInUser.Length..];
            return ctx => ctx.Request.Headers.Authorization = "Bearer " + user;
        }

        var address = IPAddress.Parse(description[AnonymousClientAt.Length..]);
        return ctx => ctx.Connection.RemoteIpAddress = address;
    }

    private static Action<HttpContext> NoClientAddressFor(string hostName) =>
        ctx =>
        {
            ctx.Connection.RemoteIpAddress = null;
            ctx.Request.Host = new HostString(hostName);
        };

    private static async Task<int> SendAsync(HostingTestHost host, Action<HttpContext> caller) =>
        (await host.SendAsync(HttpMethods.Get, HostingTestHost.AccountsPath, caller)).Response.StatusCode;

    #endregion

    /// <summary>The app's own limits: 5 requests per second for "treasury-ops", 2 for anyone else.</summary>
    private sealed class TreasuryOpsLimitProvider : IRateLimitOptionsProvider
    {
        public ConcurrencyLimiterOptions GetConcurrencyLimiterOptions(HttpContext httpContext) =>
            new() { PermitLimit = Limit(httpContext), QueueLimit = 0 };

        public FixedWindowRateLimiterOptions GetRateLimiterOptions(HttpContext httpContext) =>
            new() { PermitLimit = Limit(httpContext), QueueLimit = 0, Window = TimeSpan.FromSeconds(1) };

        private static int Limit(HttpContext httpContext) =>
            httpContext.User.Identity?.Name == "treasury-ops" ? 5 : 2;
    }
}
