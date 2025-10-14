using System.Text.Json.Serialization;
using DKNet.EfCore.Abstractions.Events;

namespace DKNet.EfCore.DtoEntities.Share;

public interface IDomainEvent : IEventItem
{
    /// <summary>
    ///     The Unit ID of the event if the same event the ID must be the same.
    /// </summary>
    public string HashId { get; }
}

public abstract record DomainEvent : EventItem, IDomainEvent
{
    private string? _hashId;

    [JsonIgnore] public string HashId => _hashId ??= GenerateHashId();

    protected abstract string GenerateHashId();
}