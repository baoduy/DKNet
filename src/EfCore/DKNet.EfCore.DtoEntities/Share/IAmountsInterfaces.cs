using System.ComponentModel.DataAnnotations;

namespace DKNet.EfCore.DtoEntities.Share;

public interface IBalanceAmounts
{
    #region Properties

    decimal AvailableAmount { get; }
    int AvailableCount { get; }

    [MaxLength(4)] string Currency { get; }

    decimal FeeAmount { get; }
    decimal PendingAmount { get; }
    int PendingCount { get; }

    #endregion
}

public interface ITransactionAmounts
{
    #region Properties

    decimal Amount { get; }

    [MaxLength(4)] string Currency { get; }

    decimal? FeeAmount { get; }
    decimal? NetAmount { get; }

    #endregion
}