using System.ComponentModel.DataAnnotations;

namespace DKNet.EfCore.DtoEntities.Share;

public interface IBalanceAmounts
{
    decimal AvailableAmount { get; }
    int AvailableCount { get; }

    [MaxLength(4)] string Currency { get; }

    decimal FeeAmount { get; }
    decimal PendingAmount { get; }
    int PendingCount { get; }
}

public interface ITransactionAmounts
{
    decimal Amount { get; }

    [MaxLength(4)] string Currency { get; }

    decimal? FeeAmount { get; }
    decimal? NetAmount { get; }
}