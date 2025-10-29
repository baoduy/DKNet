using System.Text.Json.Serialization;

namespace DKNet.EfCore.Abstractions.Events;

/// <summary>
///     Represents a domain event item that can be published.
/// </summary>
public interface IEventItem
{
    #region Properties

    /// <summary>
    ///     This additional data will be added into the message headers.
    ///     This is useful for routing or filtering messages.
    ///     And will be ignored from the Event JSON serialization.
    /// </summary>
    [JsonIgnore]
    public IDictionary<string, string> AdditionalData { get; }

    /// <summary>
    ///     Gets the type name of the event.
    /// </summary>
    public string EventType { get; }

    #endregion
}

/// <summary>
///     Base record for domain events.
/// </summary>
public abstract record EventItem : IEventItem
{
    #region Properties

    /// <inheritdoc />
    [JsonIgnore]
    public virtual IDictionary<string, string> AdditionalData { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public virtual string EventType => this.GetType().FullName ?? nameof(EventItem);

    #endregion
}