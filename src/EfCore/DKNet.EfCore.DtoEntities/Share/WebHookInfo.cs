using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.DtoEntities.Share;

[Owned]
public sealed record WebHookInfo
{
    #region Properties

    public string[] Events { get; init; } = [];
    [MaxLength(50)] public string? Secret { get; init; }

    public required Uri Url { get; init; }

    #endregion
}