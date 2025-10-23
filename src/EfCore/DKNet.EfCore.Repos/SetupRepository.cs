// ReSharper disable once CheckNamespace

namespace Microsoft.Extensions.DependencyInjection;

public static class SetupRepository
{
    /// <summary>
    ///     Add Generic Repositories. All generic repositories are required DbContext as constructor parameter.
    ///     Ensure you expose an instance of DbContext in <see cref="IServiceCollection" />.
    ///     - <see cref="IReadRepository{TEntity}" />
    ///     - <see cref="IWriteRepository{TEntity}" />
    ///     - <see cref="IRepository{TEntity}" />
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    private static IServiceCollection AddGenericRepositories(this IServiceCollection services) =>
        services
            .AddScoped(typeof(IReadRepository<>), typeof(ReadRepository<>))
            .AddScoped(typeof(IWriteRepository<>), typeof(WriteRepository<>))
            .AddScoped(typeof(IRepository<>), typeof(Repository<>));

    /// <summary>
    ///     Expose TDbContext as DbContext to <see cref="IServiceCollection" /> and Add Generic Repositories
    ///     <see cref="AddGenericRepositories" />.
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="TDbContext">
    ///     This DbContext will be exposed as default Context in dependency injection to use
    ///     Generic Repositories.
    /// </typeparam>
    /// <returns></returns>
    public static IServiceCollection AddGenericRepositories<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        if (services.All(s => s.ServiceType != typeof(DbContext)))
            services.AddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());

        return services.AddGenericRepositories();
    }

    public static IServiceCollection AddRepoFactory<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext =>
        services.AddScoped<IRepositoryFactory, RepositoryFactory<TDbContext>>();
}