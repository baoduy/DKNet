// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: EfCoreAuditLogSetup.cs
// Description: Dependency-injection setup and helpers for the EF Core audit log system.

using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.AuditLogs.Internals;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DKNet.EfCore.AuditLogs;

/// <summary>
///     Controls which entities are included in audit logging.
/// </summary>
public enum AuditLogBehaviour
{
    /// <summary>
    ///     All entities implementing <see cref="IAuditedProperties" /> are included in audit logs, unless they have
    ///     the <see cref="IgnoreAuditLogAttribute" /> applied at class level.
    /// </summary>
    IncludeAllAuditedEntities,

    /// <summary>
    ///     Only entities explicitly marked with the <see cref="AuditLogAttribute" /> and implementing
    ///     <see cref="IAuditedProperties" /> are included in audit logs. All other entities are ignored.
    /// </summary>
    OnlyAttributedAuditedEntities
}

/// <summary>
///     Internal options used to configure audit logging behaviour.
/// </summary>
internal sealed class AuditLogOptions
{
    #region Properties

    /// <summary>
    ///     Selected behaviour for which entities should be audit-logged.
    /// </summary>
    public required AuditLogBehaviour Behaviour { get; init; }

    #endregion
}

/// <summary>
///     Dependency injection helpers for registering audit logging and publishers.
///     Provides convenience methods to register the audit hook and keyed audit log publishers.
/// </summary>
public static class EfCoreAuditLogSetup
{
    #region Methods

    /// <summary>
    ///     Resolves the keyed audit log publishers registered for the given <typeparamref name="TDbContext" /> type.
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext type whose publishers should be returned.</typeparam>
    /// <param name="provider">Service provider to resolve publishers from.</param>
    /// <returns>A sequence of <see cref="IAuditLogPublisher" /> instances registered for the DbContext type.</returns>
    public static IEnumerable<IAuditLogPublisher> GetAuditLogPublishers<TDbContext>(this IServiceProvider provider)
        where TDbContext : DbContext => provider.GetKeyedServices<IAuditLogPublisher>(typeof(TDbContext).FullName);

    #endregion

    /// <param name="services">Service collection to register services into.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        ///     Adds the EF Core audit hook for <typeparamref name="TDbContext" /> which captures change information
        ///     and delegates publishing to registered <see cref="IAuditLogPublisher" /> implementations.
        /// </summary>
        /// <typeparam name="TDbContext">The application's DbContext type the hook will attach to.</typeparam>
        /// <param name="behaviour">Optional behaviour that controls which entities are included in audit logs.</param>
        /// <returns>The updated <see cref="IServiceCollection" /> for chaining.</returns>
        public IServiceCollection AddEfCoreAuditHook<TDbContext>(
            AuditLogBehaviour behaviour = AuditLogBehaviour.IncludeAllAuditedEntities)
            where TDbContext : DbContext =>
            services
                .AddSingleton(Options.Create(new AuditLogOptions { Behaviour = behaviour }))
                .AddHook<TDbContext, EfCoreAuditHook>();

        /// <summary>
        ///     Registers a keyed <see cref="IAuditLogPublisher" /> for the specified <typeparamref name="TDbContext" />.
        ///     If a publisher of the same implementation was already registered for the DbContext type this method is a no-op.
        ///     Also ensures the audit hook is registered for the DbContext.
        /// </summary>
        /// <typeparam name="TDbContext">The application's DbContext type.</typeparam>
        /// <typeparam name="TPublisher">The publisher implementation to register for the DbContext type.</typeparam>
        /// <param name="behaviour">Optional behaviour that controls which entities are included in audit logs.</param>
        /// <returns>The updated <see cref="IServiceCollection" /> for chaining.</returns>
        public IServiceCollection AddEfCoreAuditLogs<TDbContext, TPublisher>(
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
    }
}