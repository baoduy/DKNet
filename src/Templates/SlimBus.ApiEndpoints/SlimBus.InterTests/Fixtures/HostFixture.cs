using Aspire.Hosting;
using Microsoft.Extensions.Logging;
using Projects;

namespace SlimBus.InterTests.Fixtures;

public class HostFixture : IAsyncLifetime
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private DistributedApplication? _app;

    public async Task InitializeAsync()
    {
        SetupEnvironments();

        var appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<SlimBus_AppHost>();

        var envs = SetupEnvironments();
        if (envs.Count > 0)
        {
            var api = appHost.CreateResourceBuilder<ProjectResource>("Api");
            foreach (var env in envs)
                api.WithEnvironment(env.Key, env.Value);
        }

        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Debug);
        });

        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        _app = await appHost.BuildAsync().WaitAsync(DefaultTimeout);
        await _app.StartAsync().WaitAsync(DefaultTimeout);
    }

    public async Task DisposeAsync()
    {
        if (_app is null) return;
        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
    }

    protected virtual IDictionary<string, string> SetupEnvironments() => new Dictionary<string, string>();

    public async Task<HttpClient> CreateHttpClient(string resourceName)
    {
        if (_app is null) throw new InvalidOperationException("Application is not initialized.");

        var client = _app.CreateHttpClient(resourceName);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(DefaultTimeout);
        return client;
    }
}