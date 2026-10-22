// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: JobSelection.cs
// Description: The job a process was started to run, as read from its process arguments by JobSelector.

namespace DKNet.AspCore.Extensions.Jobs;

/// <summary>
///     The job a process was started to run, selected from its process arguments by <see cref="JobSelector.Select" />.
/// </summary>
/// <param name="RequestedJobName">
///     The first argument that is not an option or an option's value, as given; <see langword="null" /> when
///     there is none.
/// </param>
/// <param name="JobName">
///     The known job name, as registered, that <paramref name="RequestedJobName" /> matches regardless of letter
///     case; <see langword="null" /> unless <paramref name="IsRecognized" />.
/// </param>
/// <param name="IsRecognized">Whether <paramref name="RequestedJobName" /> matches one of <paramref name="KnownJobNames" />.</param>
/// <param name="KnownJobNames">Every known job name, for reporting an unknown job.</param>
public sealed record JobSelection(
    string? RequestedJobName,
    string? JobName,
    bool IsRecognized,
    IReadOnlyList<string> KnownJobNames)
{
    /// <summary>Whether the arguments named a job at all, recognized or not.</summary>
    /// <remarks>Written here rather than stubbed: CA1065 forbids a property that throws.</remarks>
    public bool HasJobName => RequestedJobName is not null;
}
