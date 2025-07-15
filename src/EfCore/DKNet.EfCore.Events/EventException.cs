using FluentResults;

namespace DKNet.EfCore.Events;

/// <summary>
///     Exception thrown when an event fails.
/// </summary>
/// <param name="status"></param>
public class EventException(IResultBase status)
    : Exception(status.Errors.FirstOrDefault()?.Message ?? nameof(EventException))
{
    public IResultBase Status { get; } = status;
}