// DRK-2362 §5 acceptance tests — request bounds (DRK-2374). Every scenario fails today: RequestBoundsConfig is a
// stub @ the acceptance-tests commit, so AddRequestBoundsConfig/UseRequestBoundsConfig throw NotImplementedException.
// Body-size, header-timeout and Server-header rows run on real Kestrel: TestServer ignores KestrelServerOptions
// and never sends Server, so a TestServer test would pass on code that applies no bound at all.

using System.Net;
using System.Net.Sockets;
using System.Text;
using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AspCore.Extensions.Tests.Hosting;

public class RequestBoundsConfigTests
{
    #region Methods

    private const int OneMiB = 1_048_576;

    // Scenario Outline: Request bounds refuse oversized and slow requests — row "a body maximum of 1 MiB | posts a 2 MiB body"
    [Fact]
    public async Task UseRequestBoundsConfig_BodyMaximumOneMiBAndTwoMiBBody_RefusedWith413()
    {
        await using var host = await StartBoundedHostAsync(("RequestBounds:MaxRequestBodySizeBytes", "1048576"));
        using var client = new HttpClient { BaseAddress = host.KestrelAddress };
        client.DefaultRequestHeaders.ExpectContinue = true;

        using var response = await client.PostAsync(
            HostingTestHost.AccountsPath, new ByteArrayContent(new byte[2 * OneMiB]));

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    // Scenario Outline: Request bounds refuse oversized and slow requests — row "a body maximum of 1 MiB | posts a 512 KiB body"
    [Fact]
    public async Task UseRequestBoundsConfig_BodyMaximumOneMiBAndHalfMiBBody_Accepted()
    {
        await using var host = await StartBoundedHostAsync(("RequestBounds:MaxRequestBodySizeBytes", "1048576"));
        using var client = new HttpClient { BaseAddress = host.KestrelAddress };

        using var response = await client.PostAsync(
            HostingTestHost.AccountsPath, new ByteArrayContent(new byte[512 * 1024]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Scenario Outline: Request bounds refuse oversized and slow requests — row "a request timeout of 1 second | calls a route that takes 2 seconds"
    [Fact]
    public async Task UseRequestBoundsConfig_RequestTimeoutOneSecondAndTwoSecondRoute_EndsWith504()
    {
        await using var host = await StartBoundedHostAsync(("RequestBounds:RequestTimeoutSeconds", "1"));
        using var client = new HttpClient { BaseAddress = host.KestrelAddress };

        using var response = await client.GetAsync("/v1/slow");

        response.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);
    }

    // Scenario Outline: Request bounds refuse oversized and slow requests — row "a header timeout of 1 second | sends its headers over 3 seconds"
    [Fact]
    public async Task UseRequestBoundsConfig_HeaderTimeoutOneSecondAndHeadersOverThreeSeconds_ConnectionClosed()
    {
        await using var host = await StartBoundedHostAsync(("RequestBounds:RequestHeadersTimeoutSeconds", "1"));

        (await SendHeadersSlowlyAndWaitForCloseAsync(host.KestrelAddress)).ShouldBeTrue(
            "the server must close a connection whose headers take longer than the header timeout");
    }

    // Scenario: Request bounds use their defaults when no bounds are configured
    [Fact]
    public void AddRequestBoundsConfig_NoBoundsConfigured_AppliesDefaults()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddRequestBoundsConfig(configuration);

        using var provider = services.BuildServiceProvider();
        var kestrel = provider.GetRequiredService<IOptions<KestrelServerOptions>>().Value;
        var timeouts = provider.GetRequiredService<IOptions<RequestTimeoutOptions>>().Value;

        kestrel.Limits.MaxRequestBodySize.ShouldBe(1_048_576);
        timeouts.DefaultPolicy.ShouldNotBeNull();
        timeouts.DefaultPolicy.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
        kestrel.Limits.RequestHeadersTimeout.ShouldBe(TimeSpan.FromSeconds(10));
    }

    // Scenario: Responses carry no server header
    [Fact]
    public async Task UseRequestBoundsConfig_GetAccounts_ResponseHasNoServerHeader()
    {
        await using var host = await StartBoundedHostAsync();
        using var client = new HttpClient { BaseAddress = host.KestrelAddress };

        using var response = await client.GetAsync(HostingTestHost.AccountsPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Server").ShouldBeFalse();

        // Presence check: the same Kestrel host without request bounds does send the header.
        await using var unbounded = await HostingTestHost.StartAsync(useKestrel: true);
        using var unboundedClient = new HttpClient { BaseAddress = unbounded.KestrelAddress };
        using var unboundedResponse = await unboundedClient.GetAsync(HostingTestHost.AccountsPath);
        unboundedResponse.Headers.Server.ToString().ShouldBe("Kestrel");
    }

    private static Task<HostingTestHost> StartBoundedHostAsync(params (string Key, string? Value)[] settings) =>
        HostingTestHost.StartAsync(
            b =>
            {
                HostingTestHost.AddSettings(b, settings);
                b.Services.AddRequestBoundsConfig(b.Configuration);
            },
            app =>
            {
                app.UseRequestBoundsConfig();
                app.MapGet("/v1/slow", async (HttpContext ctx) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), ctx.RequestAborted);
                    return Results.Ok();
                });
            },
            useKestrel: true);

    /// <summary>
    ///     Opens a raw connection, sends the request line and one header line per second for 3 seconds without
    ///     ever finishing the headers, then waits up to 5 more seconds. Returns whether the server closed the
    ///     connection (end of stream or reset) — any 408 it writes first is ignored.
    /// </summary>
    private static async Task<bool> SendHeadersSlowlyAndWaitForCloseAsync(Uri address)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(address.Host, address.Port);
        var stream = tcp.GetStream();

        try
        {
            await WriteAsync(stream, $"GET {HostingTestHost.AccountsPath} HTTP/1.1\r\nHost: localhost\r\n");
            for (var i = 1; i <= 3; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                await WriteAsync(stream, $"X-Slow-{i}: {i}\r\n");
            }

            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var buffer = new byte[1024];
            while (await stream.ReadAsync(buffer, deadline.Token) > 0)
            {
            }

            return true;
        }
        catch (IOException)
        {
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task WriteAsync(NetworkStream stream, string text)
    {
        await stream.WriteAsync(Encoding.ASCII.GetBytes(text));
        await stream.FlushAsync();
    }

    #endregion
}
