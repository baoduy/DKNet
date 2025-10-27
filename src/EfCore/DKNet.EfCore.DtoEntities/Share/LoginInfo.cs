using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.DtoEntities.Share;

[Owned]
public sealed class LoginInfo
{
    #region Properties

    public DateTimeOffset CreatedOn { get; init; } = DateTimeOffset.Now;

    [MaxLength(100)] public required string Email { get; init; }

    [MaxLength(50)] public Guid Id { get; init; }

    #endregion
}