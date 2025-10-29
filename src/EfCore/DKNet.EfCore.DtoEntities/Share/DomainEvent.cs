using System.Text.Json.Serialization;
using DKNet.EfCore.Abstractions.Events;

namespace DKNet.EfCore.DtoEntities.Share;

public interface IDomainEvent : IEventItem
{
    #region Properties

    /// <summary>
    ///     The Unit ID of the event if the same event the ID must be the same.
    /// </summary>
    public string HashId { get; }

    #endregion
}

public abstract record DomainEvent : EventItem, IDomainEvent
{
    #region Fields

    private string? _hashId;

    #endregion

    #region Properties

    [JsonIgnore] public string HashId => this._hashId ??= this.GenerateHashId();

    #endregion

    #region Methods

    protected abstract string GenerateHashId();

    #endregion
}