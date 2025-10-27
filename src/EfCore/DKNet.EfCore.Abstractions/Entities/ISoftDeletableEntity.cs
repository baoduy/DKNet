using System.ComponentModel.DataAnnotations;
using FluentResults;

namespace DKNet.EfCore.Abstractions.Entities;

public interface ISoftDeletableEntity
{
    #region Properties

    [MaxLength(250)] public string? DeletedBy { get; }
    public DateTimeOffset? DeletedOn { get; }
    public bool IsDeleted { get; }

    #endregion

    #region Methods

    IResultBase Delete(string byUser, DateTimeOffset? deletedOn = null);

    #endregion
}