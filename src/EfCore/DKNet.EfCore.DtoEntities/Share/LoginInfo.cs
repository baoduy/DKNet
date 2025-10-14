using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.DtoEntities.Share;

[Owned]
public sealed class LoginInfo
{
    public DateTimeOffset CreatedOn { get; init; } = DateTimeOffset.Now;

    [MaxLength(50)] public Guid Id { get; init; }

    [MaxLength(100)] public required string Email { get; init; }
}