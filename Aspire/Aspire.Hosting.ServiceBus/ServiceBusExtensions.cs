
// ReSharper disable once CheckNamespace
namespace Aspire.Hosting;

public static class ServiceBusExtensions
{
    public static IResourceBuilder<ServiceBusResource> AddServiceBus(this IDistributedApplicationBuilder builder,
        IResourceBuilder<SqlServerServerResource> sqlServer, string configFilePath, string name = "AzureBusSimulator")
    {
        var bus = new ServiceBusResource(name);

        string? connectionString;

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(bus, async (_, ct) =>
        {
            connectionString = await bus.GetConnectionStringAsync(ct).ConfigureAwait(false);

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
            .WithEnvironment("MSSQL_SA_PASSWORD", sqlServer.Resource.PasswordParameter.Value)
            .WithBindMount(configFilePath, "/ServiceBus_Emulator/ConfigFiles/Config.json", isReadOnly: true)
            .WithEndpoint(
                targetPort: 5672,
                port: 5672,
                name: ServiceBusResource.PrimaryEndpointName)
            .WithEndpoint(
                targetPort: 5671,
                port: 5671,
                name: ServiceBusResource.SecondaryEndpointName)
            .WaitFor(sqlServer);
    }
}