// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: SlimBusEventPublisher.cs
// Description: Publishes domain events to SlimMessageBus, forwarding AdditionalData as message headers when present.

using DKNet.EfCore.Abstractions.Events;
using SlimMessageBus;

namespace DKNet.SlimBus.Extensions.Handlers;

/// <summary>
///     Publishes events to an <see cref="IMessageBus" /> instance. If the event implements <see cref="IEventItem" />,
///     the publisher will copy the event's AdditionalData into message headers using case-insensitive keys.
/// </summary>
/// <remarks>
///     Creates a new <see cref="SlimBusEventPublisher" /> that will publish messages using the provided
///     <paramref name="bus" />.
/// </remarks>
/// <param name="bus">The SlimMessageBus instance used to publish events.</param>
public class SlimBusEventPublisher(IMessageBus bus) : IEventPublisher
{
    #region Fields

    private readonly IMessageBus _bus = bus ?? throw new ArgumentNullException(nameof(bus));

    #endregion

    #region Methods

    /// <summary>
    ///     Publishes the provided <paramref name="eventObj" /> to the message bus.
    ///     When <paramref name="eventObj" /> implements <see cref="IEventItem" />, any <c>AdditionalData</c>
    ///     will be forwarded as headers (case-insensitive keys).
    /// </summary>
    /// <param name="eventObj">The event object to publish.</param>
    /// <param name="cancellationToken">A cancellation token to cancel publishing.</param>
    /// <returns>A task that completes when publishing has finished.</returns>
    public virtual Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        if (eventObj is not IEventItem item) return _bus.Publish(eventObj, cancellationToken: cancellationToken);

        // Map AdditionalData to a headers dictionary (case-insensitive keys). Guard against null.
        var headers =
            item.AdditionalData.ToDictionary(kv => kv.Key, object (kv) => kv.Value, StringComparer.OrdinalIgnoreCase);

        return _bus.Publish(item, headers: headers, cancellationToken: cancellationToken);
    }

    #endregion
}