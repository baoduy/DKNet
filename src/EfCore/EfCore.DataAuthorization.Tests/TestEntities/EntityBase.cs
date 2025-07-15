using System.ComponentModel.DataAnnotations.Schema;
using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.DataAuthorization.Tests.TestEntities;

public abstract class EntityBase<TKey> : AuditedEntity<TKey>, IEventEntity, IOwnedBy
{
    [NotMapped] private readonly ICollection<object> _events = [];
    [NotMapped] private readonly ICollection<Type> _eventTypes = [];

    /// <inheritdoc />
    protected EntityBase(TKey id, string ownedBy, string createdBy, DateTimeOffset? createdOn = null)
        : base(id)
    {
        OwnedBy = ownedBy;
        SetCreatedBy(createdBy, createdOn);
    }

    public void AddEvent(object eventObj)
    {
        _events.Add(eventObj);
    }

    public void AddEvent<TEvent>() where TEvent : class
    {
        _eventTypes.Add(typeof(TEvent));
    }

    public (object[] events, Type[] eventTypes) GetEventsAndClear()
    {
        var events = _events.ToArray();
        var eventTypes = _eventTypes.ToArray();
        _events.Clear();
        _eventTypes.Clear();

        return (events, eventTypes);
    }

    public string OwnedBy { get; private set; }

    public void SetOwnedBy(string ownerKey)
    {
        OwnedBy = ownerKey;
    }

    public override string ToString() => $"{GetType().Name} '{Id}'";
}

public abstract class AggregateRoot(Guid id, string ownedBy, string createdBy, DateTimeOffset? createdOn = null)
    : EntityBase<Guid>(id, ownedBy, createdBy, createdOn);

public class Root(string name, string ownedBy) : AggregateRoot(Guid.Empty, ownedBy, $"Unit Test {Guid.NewGuid()}")
{
    public string Name { get; private set; } = name;
}