namespace DKNet.EfCore.Events.Handlers;

public interface IEventObject
{
    string EntityType { get; }
    IDictionary<string, object?> PrimaryKey { get; }
    object Event { get; }
}

internal sealed class EventObject(string entityType, IDictionary<string, object?> primaryKey, object eventItem)
    : IEventObject
{
    public string EntityType { get; } = entityType;
    public IDictionary<string, object?> PrimaryKey { get; } = primaryKey;
    public object Event { get; } = eventItem;
}