using DKNet.EfCore.DataAuthorization.Internals;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.DataAuthorization;

/// <summary>
///     Provides extension methods for configuring data authorization in Entity Framework Core.
/// </summary>
/// <remarks>
///     This class facilitates:
///     - Registration of data ownership providers
///     - Configuration of automatic data key management
///     - Integration with Entity Framework Core's dependency injection
/// </remarks>
public static class EfCoreDataAuthSetup
{
    /// <summary>
    ///     Registers a custom data ownership provider in the service collection.
    /// </summary>
    /// <typeparam name="TProvider">The type implementing <see cref="IDataOwnerProvider" />.</typeparam>
    /// <param name="services">The service collection for DI registration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    ///     This method:
    ///     - Registers the global query filter
    ///     - Sets up the data ownership provider
    ///     - Configures necessary dependencies
    /// </remarks>
    private static IServiceCollection AddDataOwnerProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IDataOwnerProvider =>
        services
            .AddGlobalModelBuilderRegister<DataOwnerAuthQueryRegister>()
            .AddScoped<IDataOwnerProvider, TProvider>();

    /// <summary>
    ///     Configures automatic data key management for a specific DbContext.
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext type to configure.</typeparam>
    /// <typeparam name="TProvider">The type implementing <see cref="IDataOwnerProvider" />.</typeparam>
    /// <param name="services">The service collection for DI registration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    ///     This method sets up:
    ///     - The data ownership provider
    ///     - Automatic key management through hooks
    ///     - Integration with the specified DbContext
    /// </remarks>
    public static IServiceCollection AddDataOwnerProvider<TDbContext, TProvider>(this IServiceCollection services)
        where TDbContext : DbContext
        where TProvider : class, IDataOwnerProvider =>
        services
            .AddDataOwnerProvider<TProvider>()
            .AddHook<TDbContext, DataOwnerHook>();
}