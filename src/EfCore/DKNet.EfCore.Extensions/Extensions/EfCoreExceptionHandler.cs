// <copyright file="EfCoreExceptionHandler.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Microsoft.Extensions.Logging;

namespace DKNet.EfCore.Extensions.Extensions;

/// <summary>
///     Represents the possible resolutions for EF Core concurrency exceptions.
/// </summary>
public enum EfConcurrencyResolution
{
    /// <summary>
    ///     Ignores changes made to the entity in case of concurrency conflict.
    /// </summary>
    IgnoreChanges,

    /// <summary>
    ///     Retries the save operation after resolving concurrency conflict.
    /// </summary>
    RetrySaveChanges,

    /// <summary>
    ///     Rethrows the concurrency exception to the caller.
    /// </summary>
    RethrowException
}

/// <summary>
///     Defines a contract for handling EF Core concurrency exceptions.
/// </summary>
public interface IEfCoreExceptionHandler
{
    /// <summary>
    ///     Gets the maximum number of retry attempts for concurrency resolution.
    /// </summary>
    public int MaxRetryCount => 3;

    /// <summary>
    ///     Handles the <see cref="DbUpdateConcurrencyException" /> and determines the resolution strategy.
    /// </summary>
    /// <param name="context">The EF Core <see cref="DbContext" /> instance.</param>
    /// <param name="exception">The concurrency exception to handle.</param>
    /// <param name="cancellationToken">A cancellation token for the async operation.</param>
    /// <returns>The concurrency resolution strategy to apply.</returns>
    Task<EfConcurrencyResolution> HandlingAsync(DbContext context, DbUpdateConcurrencyException exception,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Provides an implementation for handling EF Core concurrency exceptions and resolving conflicts.
/// </summary>
/// <param name="logger">
///     An optional logger used to record concurrency conflicts. May be <c>null</c>, in which case no logging
///     occurs; this keeps the parameterless-construction path (<c>new EfCoreExceptionHandler()</c>) working.
/// </param>
public sealed class EfCoreExceptionHandler(ILogger<EfCoreExceptionHandler>? logger = null) : IEfCoreExceptionHandler
{
    #region Methods

    /// <inheritdoc />
    public async Task<EfConcurrencyResolution> HandlingAsync(DbContext context, DbUpdateConcurrencyException exception,
        CancellationToken cancellationToken = default)
    {
        logger?.LogWarning(
            exception,
            "Concurrency conflict while saving {DbContextType}: {EntryCount} entrie(s) involved",
            context.GetType().Name,
            exception.Entries.Count);

        // DbUpdateConcurrencyException.Entries is populated for the affected-zero-rows conflict this handler
        // resolves by reloading and retrying (structured data, unlike matching EF's localisable message text).
        // Any other conflict shape carries no entries and is not safe to retry automatically.
        if (exception.Entries.Count == 0)
            return EfConcurrencyResolution.RethrowException;

        foreach (var entry in exception.Entries)
        {
            // Get the current database values for the conflicting entity
            var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken).ConfigureAwait(false);
            if (databaseValues == null) continue;

            var currentValues = entry.CurrentValues.Clone();
            entry.OriginalValues.SetValues(databaseValues);
            entry.CurrentValues.SetValues(currentValues);
        }

        return EfConcurrencyResolution.RetrySaveChanges;
    }

    #endregion
}