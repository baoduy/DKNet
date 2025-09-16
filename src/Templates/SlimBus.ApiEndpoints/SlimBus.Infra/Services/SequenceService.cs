namespace SlimBus.Infra.Services;

internal abstract class SequenceService(DbContext dbContext, Sequences sequence) : ISequenceServices
{
    public virtual async ValueTask<string> NextValueAsync() =>
        dbContext.IsSqlServer()
            ? await dbContext.NextSeqValueWithFormat(sequence)
            : Guid.NewGuid().ToString();
}