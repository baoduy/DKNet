using System.Text.Json.Serialization;

namespace DKNet.EfCore.Abstractions.Events;

public interface IEventItem
{
    public string EventType { get; }

    /// <summary>
    /// This additional data will be added into the message headers.
    /// This is useful for routing or filtering messages.
    /// And will be ignored from the Event JSON serialization.
    /// </summary>
    [JsonIgnore]
    public IDictionary<string, string> AdditionalData { get; }
}

public abstract record EventItem : IEventItem
{
    public virtual string EventType => GetType().FullName ?? nameof(EventItem);

    [JsonIgnore]
    public virtual IDictionary<string, string> AdditionalData { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}