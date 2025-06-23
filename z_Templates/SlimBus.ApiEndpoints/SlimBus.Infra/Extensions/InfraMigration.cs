namespace SlimBus.Infra.Extensions;

public static class InfraMigration
{
    public static async Task MigrateDb(string connectionString)
    {
        //Db migration
        await using var db = new CoreDbContext(new DbContextOptionsBuilder()
            .UseSqlWithMigration(connectionString)
            .UseAutoConfigModel()
            .Options);

        await db.Database.MigrateAsync();

        //TODO: Add other data seeding here. The problems with IDataSeedingConfiguration is not support owned type property.
    }
}