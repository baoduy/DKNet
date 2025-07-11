using System.ComponentModel.DataAnnotations;
using FluentResults;

namespace DKNet.EfCore.Abstractions.Entities;

public interface ISoftDeletableEntity
{
    public bool IsDeleted { get; }
    public DateTimeOffset? DeletedOn { get; }
    [MaxLength(250)] public string? DeletedBy { get; }

    IResultBase Delete(string byUser, DateTimeOffset? deletedOn = null);
}