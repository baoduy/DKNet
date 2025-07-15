using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Design;
using SlimBus.Infra.Extensions;

namespace SlimBus.Infra.Contexts;

[ExcludeFromCodeCoverage]
internal sealed class DbContextFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    public CoreDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ConnectionStrings__AppDb"] =
                    "Server=localhost;User ID=sa;Password=Pass@word1;Database=SampleDb;TrustServerCertificate=Yes;Encrypt=True;"
            })
            .Build();

        var service = new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .AddInfraServices()
            .AddLogging()
            .BuildServiceProvider();

        return service.GetRequiredService<CoreDbContext>();
    }
}