// DRK-2362 §5 acceptance tests — HTTPS and HSTS (DRK-2374). Every scenario fails today: HttpsConfig is a stub @
// the acceptance-tests commit, so AddHttpsConfig/UseHttpsConfig throw NotImplementedException. HSTS rows request
// https://api.example.com because HSTS is skipped for localhost and for plain HTTP.

using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions.Hosting;

namespace AspCore.Extensions.Tests.Hosting;

public class HttpsConfigTests
{
    #region Methods

    // Scenario: Plain HTTP is redirected to HTTPS
    [Fact]
    public async Task UseHttpsConfig_PlainHttpRequest_RedirectsToHttps()
    {
        await using var host = await StartHttpsHostAsync(("https_port", "443"));
        using var client = host.CreateClient("http://api.example.com");

        using var response = await client.GetAsync("/v1/accounts");

        ((int)response.StatusCode).ShouldBeInRange(300, 399);
        response.Headers.Location.ShouldBe(new Uri("https://api.example.com/v1/accounts"));
    }

    // Scenario Outline: HSTS asks for preload only at 365 days or more
    [Theory]
    [InlineData(null, "max-age=31536000; includeSubDomains; preload")]
    [InlineData("730", "max-age=63072000; includeSubDomains; preload")]
    [InlineData("180", "max-age=15552000; includeSubDomains")]
    public async Task UseHttpsConfig_HstsMaxAgeDays_SendsExpectedHstsHeader(string? days, string expectedHeader)
    {
        await using var host = days is null
            ? await StartHttpsHostAsync()
            : await StartHttpsHostAsync(("Https:HstsMaxAgeDays", days));
        using var client = host.CreateClient("https://api.example.com");

        using var response = await client.GetAsync("/v1/accounts");

        response.Headers.GetValues("Strict-Transport-Security").ShouldBe([expectedHeader]);
    }

    private static Task<HostingTestHost> StartHttpsHostAsync(params (string Key, string? Value)[] settings) =>
        HostingTestHost.StartAsync(
            b =>
            {
                HostingTestHost.AddSettings(b, settings);
                b.Services.AddHttpsConfig(b.Configuration);
            },
            app => app.UseHttpsConfig());

    #endregion
}
