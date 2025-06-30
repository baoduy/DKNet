using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Events.Internals;
using DKNet.EfCore.Events.Services;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class EventSetup
{
    /// <summary>
    ///     Add Event Publisher with Channel-based asynchronous processing
    /// </summary>
    /// <typeparam name="TImplementation">The implementation of <see cref="IEventPublisher" /></typeparam>
    /// <typeparam name="TDbContext"></typeparam>
    /// <param name="services"></param>
    /// <param name="channelCapacity">Maximum capacity of the event channel (default: 1000)</param>
    /// <returns></returns>
    public static IServiceCollection AddEventPublisher<TDbContext, TImplementation>(
        this IServiceCollection services,
        int channelCapacity = 1000)
        where TImplementation : class, IEventPublisher
        where TDbContext : DbContext
    {
        // Create a bounded channel for event processing
        var channel = Channel.CreateBounded<QueuedEventBatch>(new BoundedChannelOptions(channelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = true
        });

        return services
            .AddScoped<IEventPublisher, TImplementation>()
            .AddSingleton(channel)
            .AddSingleton(channel.Reader)
            .AddSingleton(channel.Writer)
            .AddHostedService<EventChannelProcessor>()
            .AddHook<TDbContext, EventHook>();
    }
}