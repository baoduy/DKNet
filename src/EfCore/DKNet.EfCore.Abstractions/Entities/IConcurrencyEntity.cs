using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DKNet.EfCore.Abstractions.Entities;

/// <summary>
///     Defines a contract for entities that support optimistic concurrency control.
/// </summary>
/// <remarks>
///     This interface provides the necessary properties and methods to implement
///     optimistic concurrency control using a row version timestamp.
/// </remarks>
public interface IConcurrencyEntity<TType>
{
    /// <summary>
    ///     Gets the row version timestamp used for concurrency checking.
    /// </summary>
    /// <value>A byte array containing the row version timestamp.</value>
    [Column(Order = 1000)]
    [Timestamp]
    TType? RowVersion { get; }

    /// <summary>
    ///     Sets the row version timestamp for the entity.
    /// </summary>
    /// <param name="rowVersion">The new row version timestamp.</param>
    void SetRowVersion(TType rowVersion);
}