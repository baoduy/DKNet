using SlimMessageBus.Host;
using SlimMessageBus.Host.Interceptor;
using DKNet.SlimBus.Extensions.Behaviors;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class SlimBusEfCoreSetup
{
    public static IServiceCollection AddSlimBusForEfCore(this IServiceCollection serviceCollection, Action<MessageBusBuilder> configure)
    {
        serviceCollection
            .AddScoped(typeof(IRequestHandlerInterceptor<,>),typeof(EfAutoSavePostProcessor<,>))
            .AddSlimMessageBus(configure);
        return serviceCollection;
    }
        
}