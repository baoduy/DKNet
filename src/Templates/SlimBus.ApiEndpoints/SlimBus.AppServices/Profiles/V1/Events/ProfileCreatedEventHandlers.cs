namespace SlimBus.AppServices.Profiles.V1.Events;

public sealed record ProfileCreatedEvent(Guid Id, string Name);

/// <summary>
///     NOTE: remove this as just for testing purposed only
/// </summary>
internal sealed class ProfileCreatedEventFromMemoryHandler : Fluents.EventsConsumers.IHandler<ProfileCreatedEvent>
{
    #region Properties

    public static bool Called { get; set; }

    #endregion

    #region Methods

    public Task OnHandle(ProfileCreatedEvent notification, CancellationToken cancellationToken)
    {
        Called = notification.Id != Guid.Empty;
        return Task.CompletedTask;
    }

    #endregion
}