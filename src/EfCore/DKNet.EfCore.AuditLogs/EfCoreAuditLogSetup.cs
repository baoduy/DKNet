using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.AuditLogs.Internals;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DKNet.EfCore.AuditLogs;

public enum AuditLogBehaviour
{
    /// <summary>
    /// All entities implementing IAuditedProperties are included in audit logs, unless they have
    /// the <see cref="IgnoreAuditLogAttribute"/> applied at class level.
    /// </summary>
    IncludeAllAuditedEntities,

    /// <summary>
    /// Only entities explicitly marked with the <see cref="AuditLogAttribute"/> and implementing
    /// IAuditedProperties are included in audit logs. All other entities are ignored.
    /// </summary>
    OnlyAttributedAuditedEntities
}

internal sealed class AuditLogOptions
{
    public required AuditLogBehaviour Behaviour { get; init; }
}

public static class EfCoreAuditLogSetup
{
    public static IServiceCollection AddEfCoreAuditHook<TDbContext>(this IServiceCollection services,
        AuditLogBehaviour behaviour = AuditLogBehaviour.IncludeAllAuditedEntities)
        where TDbContext : DbContext =>
        services
            .AddSingleton(Options.Create(new AuditLogOptions { Behaviour = behaviour }))
            .AddHook<TDbContext, EfCoreAuditHook>();

    public static IServiceCollection AddEfCoreAuditLogs<TDbContext, TPublisher>(this IServiceCollection services,
        AuditLogBehaviour behaviour = AuditLogBehaviour.IncludeAllAuditedEntities)
        where TDbContext : DbContext
        where TPublisher : class, IAuditLogPublisher
    {
        var key = typeof(TDbContext).FullName!;
        if (services.Any(s =>
                s.IsKeyedService && ReferenceEquals(s.ServiceKey, key) &&
                s.KeyedImplementationType == typeof(TPublisher)))
            return services;

        services.AddKeyedScoped<IAuditLogPublisher, TPublisher>(key);
        services.AddEfCoreAuditHook<TDbContext>(behaviour);
        return services;
    }

    public static IEnumerable<IAuditLogPublisher> GetAuditLogPublishers<TDbContext>(this IServiceProvider provider)
        where TDbContext : DbContext => provider.GetKeyedServices<IAuditLogPublisher>(typeof(TDbContext).FullName);
}