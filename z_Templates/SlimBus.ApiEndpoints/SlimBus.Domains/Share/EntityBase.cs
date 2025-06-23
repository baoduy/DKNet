using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.DataAuthorization;

namespace SlimBus.Domains.Share;

public abstract class EntityBase<TKey> : AuditedEntity<TKey>, IEventEntity, IOwnedBy
{
    private readonly Collection<object> _events = [];
    private readonly Collection<Type> _eventTypes = [];

    /// <inheritdoc />
    protected EntityBase(TKey id, string createdBy, DateTimeOffset? createdOn = null) :
        base(id, createdBy, createdOn)
    {
        SetCreatedBy(createdBy, createdOn);
    }

    /// <inheritdoc />
    protected EntityBase()
    {
    }

    public void AddEvent(object eventObj) => _events.Add(eventObj);
    public void AddEvent<TEvent>() where TEvent : class => _eventTypes.Add(typeof(TEvent));

    public (object[]events, Type[]eventTypes) GetEventsAndClear()
    {
        var events = _events.ToArray();
        var eventTypes = _eventTypes.ToArray();
        _events.Clear();
        _eventTypes.Clear();

        return (events, eventTypes);
    }

    public override string ToString() => $"{GetType().Name} '{Id}'";

    public void SetOwnedBy(string ownerKey) => OwnedBy = ownerKey;
    [StringLength(500)] public string? OwnedBy { get; private set; }
}