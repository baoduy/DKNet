using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.DtoEntities.Share;

public enum FeeTypes
{
    None,
    Percentage
}

[Owned]
public sealed class MxFee(float value, FeeTypes type)
{
    #region Properties

    public FeeTypes Type { get; private set; } = type;

    public float Value { get; private set; } = value;

    #endregion

    public static readonly MxFee Default = new(10, FeeTypes.Percentage);
}