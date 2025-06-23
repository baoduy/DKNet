using Microsoft.EntityFrameworkCore.Design;
using SlimBus.Infra.Extensions;

namespace SlimBus.Infra;

internal sealed class DbContextFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    public CoreDbContext CreateDbContext(string[] args)
    {
        var service = new ServiceCollection()
            .AddInfraServices()
            .AddLogging()
            .BuildServiceProvider();

        return service.GetRequiredService<CoreDbContext>();
    }
}