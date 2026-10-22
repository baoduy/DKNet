// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: JobSelector.cs
// Description: Selects the requested job from process arguments alone — no environment or configuration value.

namespace DKNet.AspCore.Extensions.Jobs;

/// <summary>
///     Selects the job a process was started to run from its process arguments.
/// </summary>
public static class JobSelector
{
    /// <summary>
    ///     Takes the first argument that is not an option (an option starts with <c>-</c>) as the requested job
    ///     name. The argument after a <c>--name value</c> option is that option's value, never a job name; a
    ///     <c>--name=value</c> option carries its own value. The name is matched against
    ///     <paramref name="knownJobNames" /> without regard to letter case.
    /// </summary>
    /// <param name="args">The process arguments.</param>
    /// <param name="knownJobNames">The job names the process can run.</param>
    /// <returns>The selection: no job, a recognized job, or an unknown job.</returns>
    public static JobSelection Select(IReadOnlyList<string> args, IReadOnlyCollection<string> knownJobNames)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(knownJobNames);

        string? requestedJobName = null;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.StartsWith('-'))
            {
                // "--name=value" already carries its value; only the separated "--name value" form takes the
                // next argument, and that value is never itself a job name.
                if (!arg.Contains('=', StringComparison.Ordinal)) i++;

                continue;
            }

            requestedJobName = arg;
            break;
        }

        var jobName = requestedJobName is null
            ? null
            : knownJobNames.FirstOrDefault(name =>
                string.Equals(name, requestedJobName, StringComparison.OrdinalIgnoreCase));

        return new JobSelection(requestedJobName, jobName, jobName is not null, [.. knownJobNames]);
    }
}
