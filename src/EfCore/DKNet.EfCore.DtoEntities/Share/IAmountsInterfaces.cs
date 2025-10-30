using System.ComponentModel.DataAnnotations;

namespace DKNet.EfCore.DtoEntities.Share;

public interface IBalanceAmounts
{
    #region Properties

    decimal AvailableAmount { get; }

    decimal FeeAmount { get; }

    decimal PendingAmount { get; }

    int AvailableCount { get; }

    int PendingCount { get; }

    [MaxLength(4)] string Currency { get; }

    #endregion
}

public interface ITransactionAmounts
{
    #region Properties

    decimal Amount { get; }

    decimal? FeeAmount { get; }

    decimal? NetAmount { get; }

    [MaxLength(4)] string Currency { get; }

    #endregion
}