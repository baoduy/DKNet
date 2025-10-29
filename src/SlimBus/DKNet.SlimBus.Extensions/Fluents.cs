// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: Fluents.cs
// Description: Fluent interfaces and abstractions for SlimMessageBus requests, queries, paged queries and event consumers.

using FluentResults;
using SlimMessageBus;
using X.PagedList;

namespace DKNet.SlimBus.Extensions;

/// <summary>
///     Contains fluent interfaces and abstractions for requests, queries, query handlers, and events.
/// </summary>
public static class Fluents
{
    /// <summary>
    ///     Container for event consumer handler interfaces used by SlimMessageBus integrations.
    ///     Use <see cref="EventsConsumers.IHandler{TEvent}" /> to implement event handlers.
    /// </summary>
    public static class EventsConsumers
    {
        /// <summary>
        ///     Represents a handler for an event of type <typeparamref name="TEvent" />.
        /// </summary>
        /// <typeparam name="TEvent">The type of the event.</typeparam>
        public interface IHandler<in TEvent> : IConsumer<TEvent>;
    }

    /// <summary>
    ///     Container for query-related interfaces: request/response, paged responses and query handlers.
    ///     Use the nested interfaces to implement query handlers that integrate with SlimMessageBus.
    /// </summary>
    public static class Queries
    {
        /// <summary>
        ///     Represents a handler for a query that returns a response of type <typeparamref name="TResponse" />.
        /// </summary>
        /// <typeparam name="TQuery">The type of the query.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        public interface IHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse?>
            where TQuery : IWitResponse<TResponse>;

        /// <summary>
        ///     Represents a handler for a query that returns a paged response of type <typeparamref name="TResponse" />.
        /// </summary>
        /// <typeparam name="TQuery">The type of the query.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        public interface IPageHandler<in TQuery, TResponse> : IRequestHandler<TQuery, IPagedList<TResponse>>
            where TQuery : IWitPageResponse<TResponse>;

        /// <summary>
        ///     Represents a query that returns a paged response of type <typeparamref name="TResponse" />.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        public interface IWitPageResponse<out TResponse> : IRequest<IPagedList<TResponse>>;

        /// <summary>
        ///     Represents a query that returns a response of type <typeparamref name="TResponse" />.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        public interface IWitResponse<out TResponse> : IRequest<TResponse?>;
    }

    /// <summary>
    ///     Container for request-related interfaces: requests that may or may not return results and their handlers.
    /// </summary>
    public static class Requests
    {
        /// <summary>
        ///     Represents a handler for a request that does not return a response.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        public interface IHandler<in TRequest> : IRequestHandler<TRequest, IResultBase> where TRequest : INoResponse;

        /// <summary>
        ///     Represents a handler for a request that returns a response of type <typeparamref name="TResponse" />.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        public interface IHandler<in TRequest, TResponse> : IRequestHandler<TRequest, IResult<TResponse>>
            where TRequest : IWitResponse<TResponse>;

        /// <summary>
        ///     Represents a request that does not return a response.
        /// </summary>
        public interface INoResponse : IRequest<IResultBase>;

        /// <summary>
        ///     Represents a request that returns a response of type <typeparamref name="TResponse" />.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        public interface IWitResponse<out TResponse> : IRequest<IResult<TResponse>>;
    }
}