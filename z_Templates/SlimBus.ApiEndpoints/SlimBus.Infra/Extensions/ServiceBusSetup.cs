using Azure.Messaging.ServiceBus;
using SlimBus.AppServices.Profiles.V1.Events;
using SlimBus.Infra.ExternalEvents;

namespace SlimBus.Infra.Extensions;

public static class ServiceBusSetup
{
    public static IServiceCollection AddServiceBus(this IServiceCollection service, IConfiguration configuration,
        Assembly serviceAssembly)
    {
        var busConnectionString = configuration.GetConnectionString(SharedConsts.AzureBusConnectionString)!;

        service.AddSlimBusForEfCore(mbb =>
        {
            //This is a global config for all the child buses
            mbb.AddJsonSerializer();

            //Memory bus to handle the internal MediatR-Like processes
            mbb.AddChildBus("ImMemory", me =>
            {
                me.WithProviderMemory()
                    .AutoDeclareFrom(serviceAssembly)
                    .AddServicesFromAssembly(serviceAssembly);
            });

            if (!string.IsNullOrWhiteSpace(busConnectionString))
            {
                mbb.AddChildBus("AzureBus", azb =>
                {
                    azb.AddServicesFromAssembly(typeof(InfraSetup).Assembly)
                        .WithProviderServiceBus(st =>
                    {
                        st.ConnectionString = busConnectionString;
                        st.ClientFactory = (provider, settings) =>
                            new ServiceBusClient(settings.ConnectionString, new ServiceBusClientOptions
                        {
                            TransportType = ServiceBusTransportType.AmqpTcp,
                        });

                        st.TopologyProvisioning = new ServiceBusTopologySettings
                        {
                            Enabled = false,
                        };
                        // st.TopologyProvisioning = new ServiceBusTopologySettings
                        // {
                        //     Enabled = true,
                        //     // CanConsumerCreateQueue = false,
                        //     // CanConsumerCreateTopic = false,
                        //     // CanProducerCreateTopic = false,
                        //     // CanProducerCreateQueue = false,
                        //     CanConsumerCreateSubscription = true,
                        //     CreateSubscriptionOptions = op =>
                        //     {
                        //         op.EnableBatchedOperations = true;
                        //         op.MaxDeliveryCount = 10;
                        //         op.AutoDeleteOnIdle = TimeSpan.FromDays(60);
                        //         op.DeadLetteringOnMessageExpiration = true;
                        //         op.DefaultMessageTimeToLive = TimeSpan.FromDays(15);
                        //     },
                        // };
                    });

                    azb.Produce<ProfileCreatedEvent>(o => o.DefaultTopic("profile-tp"));
                    azb.Consume<ProfileCreatedEvent>(o => o.Path("profile-tp")
                        .SubscriptionName("profile-sub")
                        .WithConsumer<ProfileCreatedEmailNotificationHandler>());
                });
            }
        });

        return service;
    }
}