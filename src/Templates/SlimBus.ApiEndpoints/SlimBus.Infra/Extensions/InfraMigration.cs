using System.Diagnostics.CodeAnalysis;
using SlimBus.Infra.Contexts;

namespace SlimBus.Infra.Extensions;

[ExcludeFromCodeCoverage]
public static class InfraMigration
{
    public static async Task MigrateDb(string connectionString)
    {
        //Db migration
        await using var db = new CoreDbContext(new DbContextOptionsBuilder<CoreDbContext>()
            .UseAutoConfigModel()
            .UseSqlWithMigration(connectionString)
            .Options);

        await db.Database.MigrateAsync();

        // Data seeding can be added here when needed (IDataSeedingConfiguration has limitations with owned types)
    }
}