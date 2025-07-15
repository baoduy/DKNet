namespace SlimBus.Infra.Services;

internal abstract class SequenceService(DbContext dbContext, Sequences sequence) : ISequenceServices
{
    public virtual async ValueTask<string> NextValueAsync() =>
        string.Equals(dbContext.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.SqlServer"
            , StringComparison.OrdinalIgnoreCase)
            ? await dbContext.NextSeqValueWithFormat(sequence)
            : Guid.NewGuid().ToString();
}