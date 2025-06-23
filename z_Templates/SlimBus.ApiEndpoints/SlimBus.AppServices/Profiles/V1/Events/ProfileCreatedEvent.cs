namespace SlimBus.AppServices.Profiles.V1.Events;

public sealed record ProfileCreatedEvent(Guid Id, string Name) : DomainEvent;