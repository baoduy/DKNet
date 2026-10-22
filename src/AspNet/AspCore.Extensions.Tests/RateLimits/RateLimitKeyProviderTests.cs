// DRK-2374 §6a D1/D2 — the rate-limit bucket per caller, and the package's default limits (§5).

using System.Net;
using System.Security.Claims;
using DKNet.AspCore.Extensions.RateLimits;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Extensions.Tests.RateLimits;

public class RateLimitKeyProviderTests
{
    #region Methods

    [Theory]
    [InlineData("treasury-ops", "203.0.113.9", "user:treasury-ops")]
    [InlineData("", "203.0.113.9", "ip:203.0.113.9")]
    [InlineData(null, "203.0.113.9", "ip:203.0.113.9")]
    [InlineData("203.0.113.9", null, "user:203.0.113.9")]
    [InlineData(null, null, "shared")]
    public void GetPartitionKey_SignedInWithName_UsesNameElseAddressElseSharedBucket(
        string? signedInName, string? address, string expectedKey)
    {
        var context = Request(signedInName, address);

        RateLimitKeyProvider.GetPartitionKey(context).ShouldBe(expectedKey);
    }

    [Fact]
    public void GetPartitionKey_PrincipalWithNoIdentity_UsesAddress()
    {
        var context = Request(null, "203.0.113.9");
        context.User = new ClaimsPrincipal();

        RateLimitKeyProvider.GetPartitionKey(context).ShouldBe("ip:203.0.113.9");
    }

    [Fact]
    public void GetPartitionKey_AnonymousRequest_IgnoresForwardedForAndHost()
    {
        var context = Request(null, null);
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.7";
        context.Request.Host = new HostString("a.example");

        RateLimitKeyProvider.GetPartitionKey(context).ShouldBe("shared");
    }

    [Fact]
    public void GetPartitionKey_UserNamedLikeAnAddressAndAnonymousCallerAtIt_GetDifferentBuckets()
    {
        var user = RateLimitKeyProvider.GetPartitionKey(Request("203.0.113.9", "10.0.0.1"));
        var anonymous = RateLimitKeyProvider.GetPartitionKey(Request(null, "203.0.113.9"));

        user.ShouldBe("user:203.0.113.9");
        anonymous.ShouldBe("ip:203.0.113.9");
    }

    [Fact]
    public void AddRateLimitConfig_NoRateLimitSection_DefaultProviderGivesPackageDefaults()
    {
        var services = new ServiceCollection();
        services.AddRateLimitConfig(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var limits = provider.GetRequiredService<IRateLimitOptionsProvider>();
        var context = new DefaultHttpContext();

        var window = limits.GetRateLimiterOptions(context);
        var inFlight = limits.GetConcurrencyLimiterOptions(context);

        window.PermitLimit.ShouldBe(2);
        window.Window.ShouldBe(TimeSpan.FromSeconds(1));
        window.QueueLimit.ShouldBe(0);
        inFlight.PermitLimit.ShouldBe(2);
        inFlight.QueueLimit.ShouldBe(0);
    }

    private static DefaultHttpContext Request(string? signedInName, string? address)
    {
        var context = new DefaultHttpContext();
        if (signedInName is not null)
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.Name, signedInName)], "Bearer"));
        context.Connection.RemoteIpAddress = address is null ? null : IPAddress.Parse(address);
        return context;
    }

    #endregion
}
