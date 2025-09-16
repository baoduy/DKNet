namespace SlimBus.AppHost.Resources;

internal static class ServiceBusExtensions
{
    public static IResourceBuilder<ServiceBusResource> AddServiceBus(this IDistributedApplicationBuilder builder,
        string name, string configFilePath, IResourceBuilder<SqlServerServerResource> sqlServer)
    {
        var bus = new ServiceBusResource(name);

        string? connectionString;

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(bus, async (eventObj, ct) =>
        {
            connectionString = await bus.GetConnectionStringAsync(ct);

            if (connectionString == null)
                throw new DistributedApplicationException(
                    $"ConnectionStringAvailableEvent was published for the '{bus.Name}' resource but the connection string was null.");
        });

        return builder.AddResource(bus)
            .WithImage("azure-messaging/servicebus-emulator")
            .WithImageRegistry("mcr.microsoft.com")
            .WithImageTag("latest")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("SQL_SERVER", sqlServer.Resource.Name)
            .WithEnvironment("MSSQL_SA_PASSWORD", sqlServer.Resource.PasswordParameter)
            .WithBindMount(configFilePath, "/ServiceBus_Emulator/ConfigFiles/Config.json", true)
            .WithEndpoint(
                targetPort: 5672,
                port: 5672,
                name: ServiceBusResource.PrimaryEndpointName)
            .WaitFor(sqlServer);
    }
}