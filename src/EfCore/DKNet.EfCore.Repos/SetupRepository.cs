// ReSharper disable once CheckNamespace

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Repo setup extensions for <see cref="IServiceCollection" />
/// </summary>
public static class SetupRepository
{
    /// <param name="services"></param>
    extension(IServiceCollection services)
    {
        /// <summary>
        ///     Add Generic Repositories. All generic repositories are required DbContext as constructor parameter.
        ///     Ensure you expose an instance of DbContext in <see cref="IServiceCollection" />.
        ///     - <see cref="IReadRepository{TEntity}" />
        ///     - <see cref="IWriteRepository{TEntity}" />
        ///     - <see cref="IRepository{TEntity}" />
        /// </summary>
        /// <returns></returns>
        private IServiceCollection AddGenericRepositories() =>
            services
                .AddScoped(typeof(IReadRepository<>), typeof(ReadRepository<>))
                .AddScoped(typeof(IWriteRepository<>), typeof(WriteRepository<>))
                .AddScoped(typeof(IRepository<>), typeof(Repository<>));

        /// <summary>
        ///     Expose TDbContext as DbContext to <see cref="IServiceCollection" /> and Add Generic Repositories
        ///     <see cref="AddGenericRepositories" />.
        /// </summary>
        /// <typeparam name="TDbContext">
        ///     This DbContext will be exposed as default Context in dependency injection to use
        ///     Generic Repositories.
        /// </typeparam>
        /// <returns></returns>
        public IServiceCollection AddGenericRepositories<TDbContext>()
            where TDbContext : DbContext
        {
            if (services.All(s => s.ServiceType != typeof(DbContext)))
                services.AddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());

            return services.AddGenericRepositories();
        }

        /// <summary>
        ///     Add Repository Factory for TDbContext
        /// </summary>
        /// <typeparam name="TDbContext"></typeparam>
        /// <returns></returns>
        public IServiceCollection AddRepoFactory<TDbContext>()
            where TDbContext : DbContext =>
            services.AddScoped<IRepositoryFactory, RepositoryFactory<TDbContext>>();
    }
}