using System.Diagnostics.CodeAnalysis;
using Azure.Messaging.ServiceBus;
using SlimBus.AppServices.Profiles.V1.Events;
using SlimBus.Infra.Features.Profiles.ExternalEvents;

namespace SlimBus.Infra.Extensions;

[ExcludeFromCodeCoverage]
public static class ServiceBusSetup
{
    private static MessageBusBuilder AddMemoryBus(this MessageBusBuilder builder, Assembly serviceAssembly)
    {
        //Memory bus to handle the internal MediatR-Like processes
        builder.AddChildBus("ImMemory", me =>
            //https://github.com/zarusz/SlimMessageBus/blob/master/docs/provider_memory.md
            me.WithProviderMemory(cf =>
                {
                    cf.EnableMessageHeaders = false;
                    cf.EnableMessageSerialization = false;
                    cf.EnableBlockingPublish = false;
                })
                .AutoDeclareFrom(serviceAssembly)
                .AddServicesFromAssembly(serviceAssembly));

        return builder;
    }

    private static MessageBusBuilder AddAzureBus(this MessageBusBuilder builder, string connectionString)
    {
        builder.AddChildBus("AzureBus", azb =>
        {
            azb.AddServicesFromAssembly(typeof(InfraSetup).Assembly)
                .WithProviderServiceBus(st =>
                {
                    st.ConnectionString = connectionString;
                    st.ClientFactory = (_, settings) =>
                        new ServiceBusClient(settings.ConnectionString, new ServiceBusClientOptions
                        {
                            // Use WebSockets transport for Azure Service Bus
                            TransportType = ServiceBusTransportType.AmqpWebSockets
                        });

                    st.TopologyProvisioning = new ServiceBusTopologySettings
                    {
                        Enabled = false,
                        CanProducerCreateTopic = true,
                        CanProducerCreateQueue = true,
                        CanConsumerCreateSubscription = true,
                        CanConsumerCreateQueue = true,
                        CreateSubscriptionOptions = op =>
                        {
                            op.EnableBatchedOperations = true;
                            op.MaxDeliveryCount = 10;
                            op.AutoDeleteOnIdle = TimeSpan.FromDays(60);
                            op.DeadLetteringOnMessageExpiration = true;
                            op.DefaultMessageTimeToLive = TimeSpan.FromDays(7);
                        }
                    };
                });

            azb.Produce<ProfileCreatedEvent>(o => o.DefaultTopic("profile-tp"));
            azb.Consume<ProfileCreatedEvent>(o => o.Path("profile-tp")
                .SubscriptionName("profile-sub")
                .WithConsumer<ProfileCreatedEmailNotificationHandler>());
        });
        return builder;
    }

    public static IServiceCollection AddServiceBus(this IServiceCollection service, IConfiguration configuration,
        Assembly serviceAssembly)
    {
        var busConnectionString = configuration.GetConnectionString(SharedConsts.AzureBusConnectionString)!;

        service.AddSlimBusForEfCore(mbb =>
        {
            //This is a global config for all the child buses
            mbb.AddJsonSerializer();

            mbb.AddMemoryBus(serviceAssembly);

            if (!string.IsNullOrWhiteSpace(busConnectionString))
                mbb.AddAzureBus(busConnectionString);
        });

        return service;
    }
}