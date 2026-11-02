using DKNet.AspCore.Tasks;
using DKNet.AspCore.Tasks.Internals;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

// ReSharper disable ClassNeverInstantiated.Local

namespace AspCore.Tasks.Tests;

public class BackgroundJobRegistrationTests
{
    #region Methods

    [Fact]
    public async Task AddBackgroundJob_JobAlreadyRegisteredAsItself_StillRuns()
    {
        var probe = new BackgroundJobScopeTests.Probe();
        using var host = new HostBuilder()
            .ConfigureServices(s =>
            {
                s.AddSingleton(probe);
                s.AddScoped<SelfRegisteredTask>();
                s.AddBackgroundJob<SelfRegisteredTask>();
            })
            .Build();

        await host.StartAsync();
        await host.Services.GetServices<IHostedService>().OfType<BackgroundJobHost>().Single().ExecuteTask!
            .WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();

        probe.OnceRuns.ShouldBe(1);
    }

    #endregion

    private sealed class SelfRegisteredTask(BackgroundJobScopeTests.Probe probe) : IBackgroundTask
    {
        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            probe.RecordRun();
            return Task.CompletedTask;
        }
    }
}
