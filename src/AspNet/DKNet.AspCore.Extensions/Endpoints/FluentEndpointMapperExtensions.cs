// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: FluentEndpointMapperExtensions.cs
// Description: Extension helpers to map HTTP endpoints to SlimMessageBus-based fluent requests/queries using minimal APIs.

using DKNet.AspCore.Extensions.Responses;
using DKNet.SlimBus.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SlimMessageBus;

namespace DKNet.AspCore.Extensions.Endpoints;

/// <summary>
///     Minimal-API endpoint mapping helpers that wire SlimMessageBus requests/queries to HTTP verbs using
///     the fluent request/query interfaces defined in the SlimBus package.
/// </summary>
public static class FluentsEndpointMapperExtensions
{
    /// <summary>
    ///     Adds a set of common response metadata to the endpoint (standard error status codes and problem details).
    /// </summary>
    /// <param name="routeBuilder">The route handler builder to add metadata to.</param>
    public static RouteHandlerBuilder ProducesCommons(this RouteHandlerBuilder routeBuilder) =>
        routeBuilder
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);

    /// <param name="app">The <see cref="RouteGroupBuilder" /> used to register the endpoint.</param>
    extension(RouteGroupBuilder app)
    {
        /// <summary>
        ///     Maps an HTTP DELETE endpoint that accepts a command producing a response of type <typeparamref name="TResponse" />.
        /// </summary>
        /// <typeparam name="TCommand">Command type implementing <see cref="Fluents.Requests.IWitResponse{TResponse}" />.</typeparam>
        /// <typeparam name="TResponse">Response type returned by the command.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapDelete<TCommand, TResponse>(string endpoint)
            where TCommand : class, Fluents.Requests.IWitResponse<TResponse>
        {
            return app.MapDelete(
                    endpoint,
                    async (IMessageBus bus, [FromBody] TCommand request) =>
                    {
                        var rs = await bus.Send(request);
                        return rs.Response();
                    }).Produces<TResponse>()
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps an HTTP DELETE endpoint that accepts a command without a response.
        /// </summary>
        /// <typeparam name="TCommand">Command type implementing <see cref="Fluents.Requests.INoResponse" />.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapDelete<TCommand>(string endpoint)
            where TCommand : class, Fluents.Requests.INoResponse
        {
            return app.MapDelete(
                endpoint,
                async (IMessageBus bus, [AsParameters] TCommand request) =>
                {
                    var rs = await bus.Send(request);
                    return rs.Response();
                }).ProducesCommons();
        }

        /// <summary>
        ///     Maps an HTTP GET endpoint that executes a query and returns a single <typeparamref name="TResponse" />.
        /// </summary>
        /// <typeparam name="TCommand">Query type implementing <see cref="Fluents.Queries.IWitResponse{TResponse}" />.</typeparam>
        /// <typeparam name="TResponse">Response type returned by the query.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapGet<TCommand, TResponse>(string endpoint)
            where TCommand : class, Fluents.Queries.IWitResponse<TResponse>
        {
            return app.MapGet(
                    endpoint,
                    async (IMessageBus bus, [AsParameters] TCommand request) =>
                    {
                        var rs = await bus.Send(request);
                        return rs is not null ? Results.Ok(rs) : Results.NotFound();
                    })
                .Produces<TResponse>()
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps an HTTP GET endpoint that executes a paged query and returns a paged result.
        /// </summary>
        /// <typeparam name="TCommand">Query type implementing <see cref="Fluents.Queries.IWitPageResponse{TResponse}" />.</typeparam>
        /// <typeparam name="TResponse">Item type contained in the paged response.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapGetPage<TCommand, TResponse>(string endpoint)
            where TCommand : class, Fluents.Queries.IWitPageResponse<TResponse>
        {
            return app.MapGet(
                    endpoint,
                    async (IMessageBus bus, [AsParameters] TCommand request) =>
                    {
                        var rs = await bus.Send(request);
                        return Results.Ok(new PagedResponse<TResponse>(rs));
                    })
                .Produces<PagedResponse<TResponse>>()
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps an HTTP PATCH endpoint that accepts a command producing a response of type <typeparamref name="TResponse" />.
        /// </summary>
        /// <typeparam name="TCommand">Command type implementing <see cref="Fluents.Requests.IWitResponse{TResponse}" />.</typeparam>
        /// <typeparam name="TResponse">Response type returned by the command.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapPatch<TCommand, TResponse>(string endpoint)
            where TCommand : class, Fluents.Requests.IWitResponse<TResponse>
        {
            return app.MapPatch(
                    endpoint,
                    async (IMessageBus bus, TCommand request) =>
                    {
                        var rs = await bus.Send(request);
                        return rs.Response();
                    }).Produces<TResponse>()
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps an HTTP PATCH endpoint that accepts a command without a response.
        /// </summary>
        /// <typeparam name="TCommand">Command type implementing <see cref="Fluents.Requests.INoResponse" />.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapPatch<TCommand>(string endpoint)
            where TCommand : class, Fluents.Requests.INoResponse
        {
            return app.MapPatch(
                endpoint,
                async (IMessageBus bus, TCommand request) =>
                {
                    var rs = await bus.Send(request);
                    return rs.Response();
                }).ProducesCommons();
        }

        /// <summary>
        ///     Maps an HTTP POST endpoint that accepts a command producing a response of type <typeparamref name="TResponse" />.
        ///     Returns 201 Created when <typeparamref name="TCommand" />'s name contains "Create" (case-insensitive);
        ///     otherwise returns 200 Ok.
        /// </summary>
        /// <typeparam name="TCommand">Command type implementing <see cref="Fluents.Requests.IWitResponse{TResponse}" />.</typeparam>
        /// <typeparam name="TResponse">Response type returned by the command.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapPost<TCommand, TResponse>(string endpoint)
            where TCommand : class, Fluents.Requests.IWitResponse<TResponse>
        {
            var isCreating = typeof(TCommand).Name.Contains("Create", StringComparison.OrdinalIgnoreCase);
            return app.MapPost(
                    endpoint,
                    async (IMessageBus bus, TCommand request) =>
                    {
                        var rs = await bus.Send(request);
                        return rs.Response(isCreating);
                    }).Produces<TResponse>(isCreating ? StatusCodes.Status201Created : StatusCodes.Status200OK)
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps an HTTP POST endpoint that accepts a command without a response.
        ///     Returns 201 Created when <typeparamref name="TCommand" />'s name contains "Create" (case-insensitive);
        ///     otherwise returns 200 Ok.
        /// </summary>
        /// <typeparam name="TCommand">Command type implementing <see cref="Fluents.Requests.INoResponse" />.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapPost<TCommand>(string endpoint)
            where TCommand : class, Fluents.Requests.INoResponse
        {
            var isCreating = typeof(TCommand).Name.Contains("Create", StringComparison.OrdinalIgnoreCase);
            return app.MapPost(
                    endpoint,
                    async (IMessageBus bus, TCommand request) =>
                    {
                        var rs = await bus.Send(request);
                        return rs.Response(isCreating);
                    }).Produces(isCreating ? StatusCodes.Status201Created : StatusCodes.Status200OK)
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps an HTTP PUT endpoint that accepts a command producing a response of type <typeparamref name="TResponse" />.
        /// </summary>
        /// <typeparam name="TCommand">Command type implementing <see cref="Fluents.Requests.IWitResponse{TResponse}" />.</typeparam>
        /// <typeparam name="TResponse">Response type returned by the command.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapPut<TCommand, TResponse>(string endpoint)
            where TCommand : class, Fluents.Requests.IWitResponse<TResponse>
        {
            return app.MapPut(
                    endpoint,
                    async (IMessageBus bus, TCommand request) =>
                    {
                        var rs = await bus.Send(request);
                        return rs.Response();
                    }).Produces<TResponse>()
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps an HTTP PUT endpoint that accepts a command without a response.
        /// </summary>
        /// <typeparam name="TCommand">Command type implementing <see cref="Fluents.Requests.INoResponse" />.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapPut<TCommand>(string endpoint)
            where TCommand : class, Fluents.Requests.INoResponse
        {
            return app.MapPut(
                endpoint,
                async (IMessageBus bus, TCommand request) =>
                {
                    var rs = await bus.Send(request);
                    return rs.Response();
                }).ProducesCommons();
        }

        /// <summary>
        ///     Maps an HTTP PUT endpoint that binds the target key from the route into the command before dispatch.
        /// </summary>
        /// <typeparam name="TCommand">
        ///     Command type implementing <see cref="Fluents.Requests.IWitResponse{TResponse}" /> and
        ///     <see cref="Fluents.Requests.IWithKey{TKey}" />.
        /// </typeparam>
        /// <typeparam name="TKey">The entity key type bound from the route.</typeparam>
        /// <typeparam name="TResponse">Response type returned by the command.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapPutById<TCommand, TKey, TResponse>(string endpoint = "{id}")
            where TCommand : class, Fluents.Requests.IWitResponse<TResponse>, Fluents.Requests.IWithKey<TKey>
        {
            return app.MapPut(
                    endpoint,
                    async (IMessageBus bus, TKey id, TCommand request) =>
                    {
                        request.Id = id;
                        var rs = await bus.Send(request);
                        return rs.Response();
                    }).Produces<TResponse>()
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps an HTTP endpoint for a given method that binds the target key from the route into the
        ///     command before dispatch.
        /// </summary>
        /// <typeparam name="TCommand">
        ///     Command type implementing <see cref="Fluents.Requests.IWitResponse{TResponse}" /> and
        ///     <see cref="Fluents.Requests.IWithKey{TKey}" />.
        /// </typeparam>
        /// <typeparam name="TKey">The entity key type bound from the route.</typeparam>
        /// <typeparam name="TResponse">Response type returned by the command.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <param name="httpMethod">The HTTP method to register (e.g. <c>"POST"</c>, <c>"PUT"</c>, <c>"PATCH"</c>).</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapActionById<TCommand, TKey, TResponse>(string endpoint, string httpMethod)
            where TCommand : class, Fluents.Requests.IWitResponse<TResponse>, Fluents.Requests.IWithKey<TKey>
        {
            return app.MapMethods(
                    endpoint,
                    [httpMethod],
                    async (IMessageBus bus, TKey id, TCommand request) =>
                    {
                        request.Id = id;
                        var rs = await bus.Send(request);
                        return rs.Response();
                    }).Produces<TResponse>()
                .ProducesCommons();
        }
    }
}