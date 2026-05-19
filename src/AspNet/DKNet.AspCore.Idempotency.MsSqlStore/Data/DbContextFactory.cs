using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.AspCore.Idempotency.MsSqlStore.Data;

[ExcludeFromCodeCoverage]
internal sealed class DbContextFactory : IDesignTimeDbContextFactory<IdempotencyDbContext>
{
    public IdempotencyDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("IDEMPOTENCY_MSSQL_CONNECTION")
                   ?? throw new InvalidOperationException(
                       "Set the IDEMPOTENCY_MSSQL_CONNECTION environment variable to run EF Core design-time tools.");

        var service = new ServiceCollection()
            .AddLogging()
            .AddIdempotencyMsSqlStore(conn)
            .BuildServiceProvider();

        return service.GetRequiredService<IdempotencyDbContext>();
    }
}
