using System.Diagnostics.CodeAnalysis;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SlimBus.Infra.Contexts;
using SlimBus.Infra.Services;

namespace SlimBus.Infra.Extensions;

[ExcludeFromCodeCoverage]
public static class InfraSetup
{
    #region Methods

    private static IServiceCollection AddImplementations(this IServiceCollection services)
    {
        services.Scan(s => s.FromAssemblies(typeof(InfraSetup).Assembly)
            .AddClasses(c => c.Where(t =>
                t is { IsSealed: true, Namespace: not null }
                && (t.Namespace!.Contains(".Repos", StringComparison.Ordinal)
                    || t.Namespace!.Contains(".Services", StringComparison.Ordinal))
            ), false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }

    public static IServiceCollection AddInfraServices(this IServiceCollection service)
    {
        service
            .AddGenericRepositories<CoreDbContext>()
            .AddImplementations()
            .AddEventPublisher<CoreDbContext, EventPublisher>()
            .AddDbContextWithHook<CoreDbContext>((sp, builder) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var conn = config.GetConnectionString(SharedConsts.DbConnectionString)!;

                builder.UseSqlWithMigration(conn)
                    .UseAutoConfigModel([typeof(CoreDbContext).Assembly])
                    .UseAutoDataSeeding([typeof(InfraSetup).Assembly]);
            });

        return service;
    }

    internal static DbContextOptionsBuilder UseSqlWithMigration(this DbContextOptionsBuilder builder,
        string connectionString)
    {
        builder.ConfigureWarnings(warnings =>
        {
            warnings.Log(RelationalEventId.PendingModelChangesWarning);
            warnings.Log(CoreEventId.ManyServiceProvidersCreatedWarning);
        });
#if DEBUG
        builder.EnableDetailedErrors().EnableSensitiveDataLogging();
#endif

        return builder.UseSqlServer(connectionString,
            o => o
                .MinBatchSize(1)
                .MaxBatchSize(100)
                .MigrationsHistoryTable(nameof(CoreDbContext), DomainSchemas.Migration)
                .MigrationsAssembly(typeof(CoreDbContext).Assembly)
                .EnableRetryOnFailure()
                .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
    }

    #endregion
}