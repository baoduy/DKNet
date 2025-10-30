// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: IAuditLogPublisher.cs
// Description: Contract for components that publish collected audit log entries to external sinks.


namespace DKNet.EfCore.AuditLogs;

/// <summary>
///     Defines a publisher responsible for persisting or forwarding audit log entries produced by the auditing pipeline.
///     Implementations may write entries to a database, message queue, or external audit service.
/// </summary>
public interface IAuditLogPublisher
{
    #region Methods

    /// <summary>
    ///     Publishes a batch of <see cref="AuditLogEntry" /> items to the target sink.
    /// </summary>
    /// <param name="logs">The list of audit log entries to publish. May be empty but not <c>null</c>.</param>
    /// <param name="cancellationToken">Cancellation token to observe while performing asynchronous publishing.</param>
    Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default);

    #endregion
}