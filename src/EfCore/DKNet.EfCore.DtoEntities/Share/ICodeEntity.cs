using System.ComponentModel.DataAnnotations;

namespace DKNet.EfCore.DtoEntities.Share;

public interface ICodeEntity
{
    #region Properties

    [MaxLength(100)] string Code { get; }

    #endregion
}