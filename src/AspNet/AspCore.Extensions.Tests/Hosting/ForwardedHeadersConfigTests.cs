// DRK-2362 §5 acceptance tests — forwarded headers (DRK-2374). Every scenario fails today: ForwardedHeadersConfig
// is a stub @ the acceptance-tests commit, so AddForwardedHeadersConfig/UseForwardedHeadersConfig throw
// NotImplementedException.

using System.Net;
using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AspCore.Extensions.Tests.Hosting;

public class ForwardedHeadersConfigTests
{
    #region Methods

    // Scenario Outline: Forwarded headers are trusted only from trusted proxies
    [Theory]
    [InlineData("the network 10.0.0.0/8", "10.1.2.3", "198.51.100.7", "https")]
    [InlineData("the network 10.0.0.0/8", "203.0.113.9", "203.0.113.9", "http")]
    [InlineData("the proxy 10.0.0.5", "10.0.0.5", "198.51.100.7", "https")]
    [InlineData("the proxy 10.0.0.5", "10.0.0.6", "10.0.0.6", "http")]
    [InlineData("no proxy and no network", "10.1.2.3", "10.1.2.3", "http")]
    public async Task UseForwardedHeadersConfig_RequestForwardedForClientOverHttps_HostSeesExpectedClientAndScheme(
        string trust, string peer, string client, string scheme)
    {
        await using var host = await HostingTestHost.StartAsync(
            b =>
            {
                HostingTestHost.AddSettings(b, Trust(trust));
                b.Services.AddForwardedHeadersConfig(b.Configuration);
            },
            app =>
            {
                app.UseForwardedHeadersConfig();
                app.MapGet("/v1/whoami", (HttpContext ctx) => $"{ctx.Connection.RemoteIpAddress} {ctx.Request.Scheme}");
            });

        var context = await host.SendAsync(HttpMethods.Get, "/v1/whoami", ctx =>
        {
            ctx.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            ctx.Request.Headers["X-Forwarded-For"] = "198.51.100.7";
            ctx.Request.Headers["X-Forwarded-Proto"] = "https";
        });

        (await HostingTestHost.ReadBodyAsync(context)).ShouldBe($"{client} {scheme}");
    }

    /// <summary>The settings for the spec's trust column: a trusted network, a trusted proxy, or neither.</summary>
    private static (string Key, string? Value)[] Trust(string trust) => trust switch
    {
        "the network 10.0.0.0/8" => [("Security:TrustedNetworks:0", "10.0.0.0/8")],
        "the proxy 10.0.0.5" => [("Security:TrustedProxies:0", "10.0.0.5")],
        _ => []
    };

    #endregion
}
