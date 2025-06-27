namespace SlimBus.AppServices.Profiles.V1.Events;

public sealed record ProfileCreatedEvent(Guid Id, string Name) : DomainEvent;

/// <summary>
/// TODO remove this as just for testing purposed only
/// </summary>
internal sealed class ProfileCreatedEventFromMemoryHandler : Fluents.Events.IHandler<ProfileCreatedEvent>
{
    public static bool Called { get; set; }

    public Task OnHandle(ProfileCreatedEvent notification, CancellationToken cancellationToken)
    {
        Called = notification.Id != Guid.Empty;
        return Task.CompletedTask;
    }
}