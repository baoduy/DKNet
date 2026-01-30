using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.AspCore.Idempotency.MsSqlStore.Data;

[ExcludeFromCodeCoverage]
internal sealed class DbContextFactory : IDesignTimeDbContextFactory<IdempotencyDbContext>
{
    #region Methods

    public IdempotencyDbContext CreateDbContext(string[] args)
    {
        var conn =
            "Server=localhost;User ID=sa;Password=Pass@word1;Database=SampleDb;TrustServerCertificate=Yes;Encrypt=True;";

        var service = new ServiceCollection()
            .AddLogging()
            .AddIdempotencyMsSqlStore(conn)
            .BuildServiceProvider();

        return service.GetRequiredService<IdempotencyDbContext>();
    }

    #endregion
}