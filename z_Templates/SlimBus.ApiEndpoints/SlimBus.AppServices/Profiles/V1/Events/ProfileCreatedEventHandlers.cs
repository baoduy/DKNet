namespace SlimBus.AppServices.Profiles.V1.Events;

/// <summary>
/// TODO remove this as just for testing purposed only
/// </summary>
public sealed class ProfileCreatedEventFromMemoryHandler : Fluents.Events.IHandler<ProfileCreatedEvent>
{
    public static bool Called { get; set; }

    public Task OnHandle(ProfileCreatedEvent notification, CancellationToken cancellationToken)
    {
        Called = notification.Id != Guid.Empty;
        return Task.CompletedTask;
    }
}