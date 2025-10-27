using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.DtoEntities.Share;

[Owned]
public sealed record ClientAppInfo
{
    #region Properties

    [MaxLength(50)] public required Guid ClientId { get; init; }
    public DateTimeOffset CreatedOn { get; init; }

    #endregion
}