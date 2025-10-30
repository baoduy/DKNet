// <copyright file="IBackgroundTask.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace DKNet.AspCore.Tasks;

/// <summary>
///     Represents a background task that runs during application startup.
/// </summary>
public interface IBackgroundTask
{
    #region Methods

    /// <summary>
    ///     Executes the background task asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the task.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RunAsync(CancellationToken cancellationToken = default);

    #endregion
}