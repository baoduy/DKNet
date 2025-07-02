using Microsoft.Extensions.Logging;
using SlimBus.AppServices.Profiles.V1.Events;
using DKNet.SlimBus.Extensions;

namespace SlimBus.Infra.ExternalEvents;

internal sealed class ProfileCreatedEmailNotificationHandler(ILogger<ProfileCreatedEmailNotificationHandler>logger): Fluents.EventsConsumers.IHandler<ProfileCreatedEvent>
{
    public static bool Called { get; set; }

    public Task OnHandle(ProfileCreatedEvent notification, CancellationToken cancellationToken)
    {
        Called = notification.Id != Guid.Empty;
        logger.LogInformation("ProfileCreatedEmailNotificationHandler called with Id: {Id}", notification.Id);
        return Task.CompletedTask;
    }
}