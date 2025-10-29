using Aspire.Hosting;
using Microsoft.Extensions.Logging;
using Projects;

namespace SlimBus.InterTests.Fixtures;

public class HostFixture : IAsyncLifetime
{
    #region Fields

    private DistributedApplication? _app;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    #endregion

    #region Methods

    public async Task<HttpClient> CreateHttpClient(string resourceName)
    {
        if (this._app is null)
        {
            throw new InvalidOperationException("Application is not initialized.");
        }

        var client = this._app.CreateHttpClient(resourceName);
        await this._app.ResourceNotifications.WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(DefaultTimeout);
        return client;
    }

    public async Task DisposeAsync()
    {
        if (this._app is null)
        {
            return;
        }

        await this._app.StopAsync();
        await this._app.DisposeAsync();
        this._app = null;
    }

    public async Task InitializeAsync()
    {
        this.SetupEnvironments();

        var appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<SlimBus_AppHost>();

        var envs = this.SetupEnvironments();
        if (envs.Count > 0)
        {
            var api = appHost.CreateResourceBuilder<ProjectResource>("Api");
            foreach (var env in envs)
            {
                api.WithEnvironment(env.Key, env.Value);
            }
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

        this._app = await appHost.BuildAsync().WaitAsync(DefaultTimeout);
        await this._app.StartAsync().WaitAsync(DefaultTimeout);
    }

    protected virtual IDictionary<string, string> SetupEnvironments() => new Dictionary<string, string>();

    #endregion
}