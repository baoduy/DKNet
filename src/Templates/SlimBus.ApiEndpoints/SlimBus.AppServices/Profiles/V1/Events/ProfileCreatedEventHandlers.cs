namespace SlimBus.AppServices.Profiles.V1.Events;

public sealed record ProfileCreatedEvent(Guid Id, string Name) : DomainEvent;

/// <summary>
/// NOTE: remove this as just for testing purposed only
/// </summary>
internal sealed class ProfileCreatedEventFromMemoryHandler : Fluents.EventsConsumers.IHandler<ProfileCreatedEvent>
{
    public static bool Called { get; set; }

    public Task OnHandle(ProfileCreatedEvent notification, CancellationToken cancellationToken)
    {
        Called = notification.Id != Guid.Empty;
        return Task.CompletedTask;
    }
}