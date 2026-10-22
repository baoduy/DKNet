// DRK-2362 §5 acceptance test — startup announcements (DRK-2374). Fails today: RateLimitConfig, HttpsConfig and
// CorsConfig are stubs @ the acceptance-tests commit, so their Add…/Use… methods throw NotImplementedException.
// Console.Out is process-wide, so this class runs alone (its collection disables parallelization) and restores it.

using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions.Hosting;
using DKNet.AspCore.Extensions.RateLimits;

namespace AspCore.Extensions.Tests.Hosting;

[CollectionDefinition(nameof(ConsoleCaptureCollection), DisableParallelization = true)]
public sealed class ConsoleCaptureCollection;

[Collection(nameof(ConsoleCaptureCollection))]
public class StartupAnnouncementTests
{
    #region Methods

    // Scenario: Turned-on items announce themselves through the host's logger
    [Fact]
    public async Task UseConfigs_RateLimitingHttpsAndCorsTurnedOn_LoggerRecordsOneEnabledLineEach()
    {
        var originalOut = Console.Out;
        using var console = new StringWriter();
        Console.SetOut(console);
        IReadOnlyCollection<string> messages;
        try
        {
            await using var host = await HostingTestHost.StartAsync(
                b =>
                {
                    HostingTestHost.AddSettings(b, ("Cors:AllowedOrigins:0", "https://app.example.com"));
                    b.Services.AddRateLimitConfig(b.Configuration);
                    b.Services.AddHttpsConfig(b.Configuration);
                    b.Services.AddCorsConfig(b.Configuration);
                },
                app =>
                {
                    app.UseHttpsConfig();
                    app.UseCorsConfig();
                    app.UseRateLimitConfig();
                });
            messages = [.. host.Logs.Entries.Select(e => e.Message)];
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        messages.Count(m => m == "RateLimitConfig enabled").ShouldBe(1);
        messages.Count(m => m == "HttpsConfig enabled").ShouldBe(1);
        messages.Count(m => m == "CorsConfig enabled").ShouldBe(1);

        var written = console.ToString();
        written.ShouldNotContain("RateLimitConfig");
        written.ShouldNotContain("HttpsConfig");
        written.ShouldNotContain("CorsConfig");
        written.ShouldNotContain("enabled");
    }

    #endregion
}
