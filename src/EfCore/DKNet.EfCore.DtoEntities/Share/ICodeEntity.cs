using System.ComponentModel.DataAnnotations;

namespace DKNet.EfCore.DtoEntities.Share;

public interface ICodeEntity
{
    [MaxLength(100)] string Code { get; }
}