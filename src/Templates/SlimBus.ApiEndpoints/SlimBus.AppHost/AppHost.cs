using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("Redis");
var sql = builder.AddSqlServer("SqlServer")
    .WithImageTag("2022-latest");

var apDb = sql
    .AddDatabase("AppDb");

builder.AddProject<SlimBus_Api>("Api")
    .WithReference(cache, "Redis")
    .WithReference(apDb, "AppDb")

    //.WaitFor(bus)
    .WaitFor(cache)
    .WaitFor(apDb);

await builder.Build().RunAsync();