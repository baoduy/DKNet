using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.Events.Internals;
using Microsoft.EntityFrameworkCore;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Event Setup
/// </summary>
public static class EventSetup
{
    #region Methods

    /// <summary>
    ///     Add Event Publisher
    /// </summary>
    /// <typeparam name="TImplementation">The implementation of <see cref="IEventPublisher" /></typeparam>
    /// <typeparam name="TDbContext"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddEventPublisher<TDbContext, TImplementation>(this IServiceCollection services)
        where TImplementation : class, IEventPublisher
        where TDbContext : DbContext =>
        services
            .AddScoped<IEventPublisher, TImplementation>()
            .AddHook<TDbContext, EventHook>();

    #endregion
}