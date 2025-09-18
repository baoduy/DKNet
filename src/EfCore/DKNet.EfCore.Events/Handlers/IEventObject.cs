namespace DKNet.EfCore.Events.Handlers;

public interface IEventObject
{
    public string EntityType { get; }
    public IDictionary<string, object?> PrimaryKey { get; }
    public object[] Events { get; }
}

internal sealed class EventObject(string entityType, IDictionary<string, object?> primaryKey, object[] events)
    : IEventObject
{
    public string EntityType { get; } = entityType;
    public IDictionary<string, object?> PrimaryKey { get; } = primaryKey;
    public object[] Events { get; } = events;
}