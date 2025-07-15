using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("Redis");
var sql = builder.AddSqlServer("SqlServer");
//var bus = builder.AddServiceBus("ServiceBus", "./Configs/busConfig.json", sql);

var apDb = sql
    .AddDatabase("AppDb");

builder.AddProject<SlimBus_Api>("Api")
    .WithReference(cache, "Redis")
    .WithReference(apDb, "AppDb")
    //.WithReference(bus, "AzureBus")
    //.WaitFor(bus)
    .WaitFor(cache)
    .WaitFor(apDb);

await builder.Build().RunAsync();