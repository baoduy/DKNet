// DRK-2374 §6a input-domain rows the frozen acceptance tests leave open (D4–D7), and rule R1 for every config.

using System.Net;
using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions.Hosting;
using DKNet.AspCore.Extensions.RateLimits;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AspCore.Extensions.Tests.Hosting;

public class HostingConfigInputDomainTests
{
    #region Methods

    // R1: Use…Config with no matching Add…Config changes nothing and logs nothing.
    [Theory]
    [InlineData(nameof(RateLimitConfig))]
    [InlineData(nameof(RequestBoundsConfig))]
    [InlineData(nameof(HttpsConfig))]
    [InlineData(nameof(ForwardedHeadersConfig))]
    [InlineData(nameof(CorsConfig))]
    public async Task UseConfig_WithoutAddConfig_AnswersAsBeforeAndLogsNothing(string config)
    {
        Action<WebApplication> use = config switch
        {
            nameof(RateLimitConfig) => app => app.UseRateLimitConfig(),
            nameof(RequestBoundsConfig) => app => app.UseRequestBoundsConfig(),
            nameof(HttpsConfig) => app => app.UseHttpsConfig(),
            nameof(ForwardedHeadersConfig) => app => app.UseForwardedHeadersConfig(),
            _ => app => app.UseCorsConfig()
        };
        await using var host = await HostingTestHost.StartAsync(configurePipeline: use);
        using var client = host.CreateClient();

        using var response = await client.GetAsync(HostingTestHost.AccountsPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        host.Logs.Entries.Select(e => e.Message).ShouldNotContain($"{config} enabled");
    }

    // §3 #9 for the two configs the startup-announcement scenario leaves out.
    [Theory]
    [InlineData(nameof(RequestBoundsConfig))]
    [InlineData(nameof(ForwardedHeadersConfig))]
    public async Task UseConfig_AfterAddConfig_LogsOneEnabledLine(string config)
    {
        await using var host = await HostingTestHost.StartAsync(
            b =>
            {
                if (config == nameof(RequestBoundsConfig))
                    b.Services.AddRequestBoundsConfig(b.Configuration);
                else
                    b.Services.AddForwardedHeadersConfig(b.Configuration);
            },
            app =>
            {
                if (config == nameof(RequestBoundsConfig))
                    app.UseRequestBoundsConfig();
                else
                    app.UseForwardedHeadersConfig();
            });

        host.Logs.Entries.Count(e => e.Message == $"{config} enabled").ShouldBe(1);
    }

    // D4: a partial section keeps the defaults for the keys it leaves out.
    [Fact]
    public void AddRequestBoundsConfig_OnlyRequestTimeoutConfigured_KeepsOtherDefaults()
    {
        using var provider = Build(
            s => s.AddRequestBoundsConfig(Settings(("RequestBounds:RequestTimeoutSeconds", "5"))));
        var kestrel = provider.GetRequiredService<IOptions<KestrelServerOptions>>().Value;
        var timeouts = provider.GetRequiredService<IOptions<RequestTimeoutOptions>>().Value;

        timeouts.DefaultPolicy!.Timeout.ShouldBe(TimeSpan.FromSeconds(5));
        timeouts.DefaultPolicy.TimeoutStatusCode.ShouldBe(StatusCodes.Status504GatewayTimeout);
        kestrel.Limits.MaxRequestBodySize.ShouldBe(1_048_576);
        kestrel.Limits.RequestHeadersTimeout.ShouldBe(TimeSpan.FromSeconds(10));
        kestrel.AddServerHeader.ShouldBeFalse();
    }

    // D5: preload only at 365 days or more — the boundary rows.
    [Theory]
    [InlineData("365", 365, true)]
    [InlineData("364", 364, false)]
    public void AddHttpsConfig_HstsMaxAgeDaysAtBoundary_PreloadOnlyFrom365(string days, int expectedDays, bool preload)
    {
        using var provider = Build(s => s.AddHttpsConfig(Settings(("Https:HstsMaxAgeDays", days))));
        var hsts = provider.GetRequiredService<IOptions<HstsOptions>>().Value;

        hsts.MaxAge.ShouldBe(TimeSpan.FromDays(expectedDays));
        hsts.Preload.ShouldBe(preload);
        hsts.IncludeSubDomains.ShouldBeTrue();
    }

    [Fact]
    public void AddHttpsConfig_ConfigureOptionsGiven_UsesThemInPlaceOfSettings()
    {
        using var provider = Build(s => s.AddHttpsConfig(
            Settings(("Https:HstsMaxAgeDays", "730")),
            o => o.MaxAge = TimeSpan.FromDays(7)));
        var hsts = provider.GetRequiredService<IOptions<HstsOptions>>().Value;

        hsts.MaxAge.ShouldBe(TimeSpan.FromDays(7));
        hsts.Preload.ShouldBeFalse();
        hsts.IncludeSubDomains.ShouldBeFalse();
    }

    // D6: proxies and networks both configured are both trusted, and nothing else is.
    [Fact]
    public void AddForwardedHeadersConfig_ProxyAndNetworkConfigured_TrustsExactlyBoth()
    {
        using var provider = Build(s => s.AddForwardedHeadersConfig(Settings(
            ("Security:TrustedProxies:0", "10.0.0.5"),
            ("Security:TrustedNetworks:0", "192.168.0.0/16"))));
        var forwarded = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        forwarded.ForwardedHeaders.ShouldBe(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        forwarded.KnownProxies.ShouldBe([IPAddress.Parse("10.0.0.5")]);
        forwarded.KnownIPNetworks.ShouldBe([System.Net.IPNetwork.Parse("192.168.0.0/16")]);
    }

    // D6: a malformed entry fails at registration, not at the first request.
    [Theory]
    [InlineData("Security:TrustedProxies:0")]
    [InlineData("Security:TrustedNetworks:0")]
    public void AddForwardedHeadersConfig_MalformedEntry_ThrowsFormatException(string key)
    {
        var services = new ServiceCollection();

        Should.Throw<FormatException>(() => services.AddForwardedHeadersConfig(Settings((key, "not-an-ip"))));
        services.ShouldBeEmpty();
    }

    // D7 / R4: blank origins turn CORS off — nothing registered, nothing logged, no CORS answer.
    [Fact]
    public async Task UseCorsConfig_OnlyBlankOrigins_CorsStaysOffAndLogsNothing()
    {
        await using var host = await HostingTestHost.StartAsync(
            b =>
            {
                HostingTestHost.AddJsonSettings(b, """{ "Cors": { "AllowedOrigins": ["", " "] } }""");
                b.Services.AddCorsConfig(b.Configuration);
            },
            app => app.UseCorsConfig());
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, HostingTestHost.AccountsPath);
        request.Headers.Add("Origin", "https://app.example.com");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
        host.Logs.Entries.Select(e => e.Message).ShouldNotContain("CorsConfig enabled");
    }

    private static IConfiguration Settings(params (string Key, string? Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

    private static ServiceProvider Build(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);
        return services.BuildServiceProvider();
    }

    #endregion
}
