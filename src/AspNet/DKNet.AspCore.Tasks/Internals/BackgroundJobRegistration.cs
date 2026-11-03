namespace DKNet.AspCore.Tasks.Internals;

/// <summary>
///     One registered start-up job. <see cref="BackgroundJobHost" /> resolves <see cref="JobType" /> from a scope of
///     its own, so concurrent jobs never share a scoped dependency.
/// </summary>
/// <param name="JobType">The concrete <see cref="IBackgroundTask" /> type, registered scoped as itself.</param>
internal sealed record BackgroundJobRegistration(Type JobType);
