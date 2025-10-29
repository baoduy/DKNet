// <copyright file="ServiceBusExtensions.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ServiceBus;

/// <summary>
///     Provides extension methods for adding Azure Service Bus emulator to Aspire applications.
/// </summary>
public static class ServiceBusExtensions
{
    #region Methods

    /// <summary>
    ///     Adds an Azure Service Bus emulator resource to the distributed application builder.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="sqlServer">The SQL Server resource builder that the Service Bus emulator depends on.</param>
    /// <param name="configFilePath">The path to the Service Bus emulator configuration file.</param>
    /// <param name="name">The name of the Service Bus resource. Default is "AzureBusSimulator".</param>
    /// <returns>A resource builder for the Service Bus resource.</returns>
    /// <exception cref="DistributedApplicationException">
    ///     Thrown when the connection string is null after the connection string
    ///     available event is published.
    /// </exception>
    public static IResourceBuilder<ServiceBusResource> AddServiceBus(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<SqlServerServerResource> sqlServer,
        string configFilePath,
        string name = "AzureBusSimulator")
    {
        var bus = new ServiceBusResource(name);

        string? connectionString;

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(
            bus,
            async (_, ct) =>
            {
                connectionString = await bus.GetConnectionStringAsync(ct);

                if (connectionString == null)
                {
                    throw new DistributedApplicationException(
                        $"ConnectionStringAvailableEvent was published for the '{bus.Name}' resource but the connection string was null.");
                }
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
            .WithEndpoint(
                targetPort: 5671,
                port: 5671,
                name: ServiceBusResource.SecondaryEndpointName)
            .WaitFor(sqlServer);
    }

    #endregion
}