using DKNet.EfCore.AuditLogs.Internals;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.AuditLogs;

public static class EfCoreAuditLogSetup
{
    public static void AddEfCoreAuditHook<TDbContext>(this IServiceCollection services) where TDbContext : DbContext
    {
        services.AddHook<TDbContext, EfCoreAuditHook>();
    }

    public static void AddEfCoreAuditLogs<TDbContext, TPublisher>(this IServiceCollection services)
        where TDbContext : DbContext
        where TPublisher : class, IAuditLogPublisher
    {
        var key = typeof(TDbContext).FullName!;
        if (services.Any(s =>
                s.IsKeyedService && ReferenceEquals(s.ServiceKey, key) &&
                s.KeyedImplementationType == typeof(TPublisher)))
            return;
        services.AddKeyedScoped<IAuditLogPublisher, TPublisher>(key);
        services.AddEfCoreAuditHook<TDbContext>();
    }

    public static IEnumerable<IAuditLogPublisher> GetAuditLogPublishers<TDbContext>(this IServiceProvider provider)
        where TDbContext : DbContext => provider.GetKeyedServices<IAuditLogPublisher>(typeof(TDbContext).FullName);
}