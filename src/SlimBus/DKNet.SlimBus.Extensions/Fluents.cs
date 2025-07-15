using FluentResults;
using SlimMessageBus;
using X.PagedList;

namespace DKNet.SlimBus.Extensions;

/// <summary>
///     Contains fluent interfaces and abstractions for requests, queries, query handlers, and events.
/// </summary>
public static class Fluents
{
    public static class Requests
    {
        /// <summary>
        ///     Represents a request that does not return a response.
        /// </summary>
        public interface INoResponse : IRequest<IResultBase>;

        /// <summary>
        ///     Represents a request that returns a response of type <typeparamref name="TResponse" />.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        public interface IWitResponse<out TResponse> : IRequest<IResult<TResponse>>;

        /// <summary>
        ///     Represents a handler for a request that does not return a response.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        public interface IHandler<in TRequest> : IRequestHandler<TRequest, IResultBase>;

        /// <summary>
        ///     Represents a handler for a request that returns a response of type <typeparamref name="TResponse" />.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        public interface IHandler<in TRequest, TResponse> : IRequestHandler<TRequest, IResult<TResponse>>;
    }

    public static class Queries
    {
        /// <summary>
        ///     Represents a query that returns a response of type <typeparamref name="TResponse" />.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        public interface IWitResponse<out TResponse> : IRequest<TResponse?>;

        /// <summary>
        ///     Represents a query that returns a paged response of type <typeparamref name="TResponse" />.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        public interface IWitPageResponse<out TResponse> : IRequest<IPagedList<TResponse>>;

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
    }

    public static class EventsConsumers
    {
        /// <summary>
        ///     Represents a handler for an event of type <typeparamref name="TEvent" />.
        /// </summary>
        /// <typeparam name="TEvent">The type of the event.</typeparam>
        public interface IHandler<in TEvent> : IConsumer<TEvent>;
    }
}