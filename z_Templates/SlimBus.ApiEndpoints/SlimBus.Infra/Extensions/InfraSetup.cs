using Microsoft.EntityFrameworkCore.Diagnostics;
using SlimBus.Infra.Core;
using DKNet.EfCore.Hooks;
using System.Diagnostics.CodeAnalysis;

namespace SlimBus.Infra.Extensions;

[ExcludeFromCodeCoverage]
public static class InfraSetup
{
    public static IServiceCollection AddInfraServices(this IServiceCollection service)
    {
        service
            .AddGenericRepositories<CoreDbContext>()
            .AddImplementations()
            .AddEventPublisher<CoreDbContext, EventPublisher>()
            .AddDbContextWithHook<CoreDbContext>((sp,builder) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var conn = config.GetConnectionString(SharedConsts.DbConnectionString)!;

                builder.UseSqlWithMigration(conn)
                    .UseAutoConfigModel(o => o.ScanFrom(typeof(InfraSetup).Assembly, typeof(DomainSchemas).Assembly));
            });

        return service;
    }

    private static IServiceCollection AddImplementations(this IServiceCollection services)
    {
        services.Scan(s => s.FromAssemblies(typeof(InfraSetup).Assembly)
            .AddClasses(c => c.Where(t =>
                t.IsSealed && t.Namespace is not null
                && (t.Namespace!.Contains(".Repos", StringComparison.Ordinal)
                    || t.Namespace!.Contains(".Services", StringComparison.Ordinal))
            ), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }

    internal static DbContextOptionsBuilder UseSqlWithMigration(this DbContextOptionsBuilder builder,
        string connectionString)
    {
        //TODO: Workaround solution to ignored the error due to this bug https://github.com/dotnet/efcore/issues/35110;
        builder.ConfigureWarnings(x => x.Ignore(RelationalEventId.PendingModelChangesWarning));
        builder.ConfigureWarnings(x => x.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

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

}