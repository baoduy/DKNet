namespace SlimBus.Api.Configs;

internal static class DbMigration
{
    public static async Task RunMigrationAsync(this WebApplicationBuilder builder, FeatureOptions features, params string[] args)
    {
#if DEBUG
        var isMigration = features.RunDbMigrationWhenAppStart;
#else
        var isMigration = args.Any(x => string.Equals(x, "migration", StringComparison.OrdinalIgnoreCase));
#endif

        if (isMigration)
        {
            Console.WriteLine("Running Db migration...");
            await InfraMigration.MigrateDb(builder.Configuration.GetConnectionString(SharedConsts.DbConnectionString)!);
            Console.WriteLine("Db migration is completed");

#if !DEBUG
                Environment.Exit(0);
#endif
        }
    }
}