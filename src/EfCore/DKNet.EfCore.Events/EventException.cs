using FluentResults;

namespace DKNet.EfCore.Events;

/// <summary>
///     Exception thrown when an event fails.
/// </summary>
/// <param name="status"></param>
public class EventException(IResultBase status)
    : Exception(status.Errors.Any() ? status.Errors[0].Message : "An error occurred during event processing.")
{
    public IResultBase Status { get; } = status;
}