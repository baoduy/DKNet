using DKNet.SlimBus.Extensions.Behaviors;
using DKNet.SlimBus.Extensions.Handlers;
using Microsoft.EntityFrameworkCore;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Interceptor;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class SlimBusEfCoreSetup
{
    #region Methods

    public static IServiceCollection AddSlimBusEventPublisher<TDbContext>(this IServiceCollection serviceCollection)
        where TDbContext : DbContext
    {
        serviceCollection
            .AddEventPublisher<TDbContext, SlimBusEventPublisher>();
        return serviceCollection;
    }

    public static IServiceCollection AddSlimBusForEfCore(
        this IServiceCollection serviceCollection,
        Action<MessageBusBuilder> configure)
    {
        serviceCollection
            .AddScoped(typeof(IRequestHandlerInterceptor<,>), typeof(EfAutoSavePostProcessor<,>))
            .AddSlimMessageBus(configure);
        return serviceCollection;
    }

    #endregion
}