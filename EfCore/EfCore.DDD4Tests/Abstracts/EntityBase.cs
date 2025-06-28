using System.ComponentModel.DataAnnotations.Schema;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.DataAuthorization;

namespace EfCore.DDD4Tests.Abstracts;

public abstract class EntityBase<TKey> : AuditedEntity<TKey>, IEventEntity, IOwnedBy
{
    [NotMapped] private readonly ICollection<object> _events = [];
    [NotMapped] private readonly ICollection<Type> _eventTypes = [];

    /// <inheritdoc />
    protected EntityBase(TKey id, string ownedBy, string createdBy, DateTimeOffset? createdOn = null) : base(id, createdBy,
        createdOn)
    {
        OwnedBy = ownedBy;
    }

    public void AddEvent(object eventObj) => _events.Add(eventObj);
    public void AddEvent<TEvent>() where TEvent : class => _eventTypes.Add(typeof(TEvent));

    public (object[] events, Type[] eventTypes) GetEventsAndClear()
    {
        var events = _events.ToArray();
        var eventTypes = _eventTypes.ToArray();
        _events.Clear();
        _eventTypes.Clear();

        return (events, eventTypes);
    }

    public override string ToString() => $"{GetType().Name} '{Id}'";
    public string OwnedBy { get; private set; }
    public void SetOwnedBy(string ownerKey) => OwnedBy = ownerKey;
}