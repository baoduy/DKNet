using DKNet.AspCore.Tasks;
using DKNet.AspCore.Tasks.Internals;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

// ReSharper disable ClassNeverInstantiated.Local

namespace AspCore.Tasks.Tests;

/// <summary>
///     A job the consumer already registered by hand still runs exactly once once it is added through
///     <c>AddBackgroundJob</c>.
/// </summary>
public class BackgroundJobRegistrationTests
{
    #region Methods

    [Fact]
    public async Task AddBackgroundJob_JobAlreadyRegisteredAsItself_RunsOnce()
    {
        var probe = await RunHostAsync(s => s.AddScoped<SelfRegisteredTask>());

        probe.OnceRuns.ShouldBe(1);
    }

    [Fact]
    public async Task AddBackgroundJob_JobAlreadyMappedToIBackgroundTask_RunsOnce()
    {
        var probe = await RunHostAsync(s => s.AddScoped<IBackgroundTask, SelfRegisteredTask>());

        probe.OnceRuns.ShouldBe(1);
    }

    private static async Task<BackgroundJobScopeTests.Probe> RunHostAsync(Action<IServiceCollection> registerByHand)
    {
        var probe = new BackgroundJobScopeTests.Probe();
        using var host = new HostBuilder()
            .ConfigureServices(s =>
            {
                s.AddSingleton(probe);
                registerByHand(s);
                s.AddBackgroundJob<SelfRegisteredTask>();
            })
            .Build();

        await host.StartAsync();
        await host.Services.GetServices<IHostedService>().OfType<BackgroundJobHost>().Single().ExecuteTask!
            .WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();
        return probe;
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
