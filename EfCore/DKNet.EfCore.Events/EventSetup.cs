using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Events.Internals;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class EventSetup
{
    /// <summary>
    ///     Add Event Publisher
    /// </summary>
    /// <typeparam name="TImplementation">The implementation of <see cref="IEventPublisher" /></typeparam>
    /// <typeparam name="TDbContext"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddEventPublisher<TDbContext, TImplementation>(this IServiceCollection services)
        where TImplementation : class, IEventPublisher
        where TDbContext : DbContext
        => services
            .AddScoped<IEventPublisher, TImplementation>()
            .AddHook<TDbContext, EventHook>();
}