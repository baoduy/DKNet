namespace SlimBus.Domains.Services;

public interface ISequenceServices : IDomainService
{
    ValueTask<string> NextValueAsync();
}