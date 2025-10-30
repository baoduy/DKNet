// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: FluentEndpointMapperExtensions.cs
// Description: Extension helpers to map HTTP endpoints to SlimMessageBus-based fluent requests/queries using minimal APIs.

using DKNet.AspCore.SlimBus;
using DKNet.SlimBus.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SlimMessageBus;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Builder;

/// <summary>
///     Minimal-API endpoint mapping helpers that wire SlimMessageBus requests/queries to HTTP verbs using
///     the fluent request/query interfaces defined in the SlimBus package.
/// </summary>
public static class FluentsEndpointMapperExtensions
{
    #region Methods

    /// <summary>
    ///     Maps an HTTP DELETE endpoint that accepts a command producing a response of type <typeparamref name="TResponse" />.
    /// </summary>
    /// <typeparam name="TCommand">Command type implementing <see cref="Fluents.Requests.IWitResponse{TResponse}" />.</typeparam>
    /// <typeparam name="TResponse">Response type returned by the command.</typeparam>
    /// <param name="app">The <see cref="RouteGroupBuilder" /> used to register the endpoint.</param>
    /// <param name="endpoint">The URL template for the endpoint.</param>
    /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
    public static RouteHandlerBuilder MapDelete<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.IWitResponse<TResponse>
    {
        return app.MapDelete(
                endpoint,
                async (IMessageBus bus, TCommand request) =>
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
    /// <param name="app">The <see cref="RouteGroupBuilder" /> used to register the endpoint.</param>
    /// <param name="endpoint">The URL template for the endpoint.</param>
    /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
    public static RouteHandlerBuilder MapDelete<TCommand>(this RouteGroupBuilder app, string endpoint)
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
    /// <param name="app">The <see cref="RouteGroupBuilder" /> used to register the endpoint.</param>
    /// <param name="endpoint">The URL template for the endpoint.</param>
    /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
    public static RouteHandlerBuilder MapGet<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
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
    /// <param name="app">The <see cref="RouteGroupBuilder" /> used to register the endpoint.</param>
    /// <param name="endpoint">The URL template for the endpoint.</param>
    /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
    public static RouteHandlerBuilder MapGetPage<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Queries.IWitPageResponse<TResponse>
    {
        return app.MapGet(
                endpoint,
                async (IMessageBus bus, [AsParameters] TCommand request) =>
                {
                    var rs = await bus.Send(request);
                    return Results.Ok(new PagedResult<TResponse>(rs));
                })
            .Produces<PagedResult<TResponse>>()
            .ProducesCommons();
    }

    /// <summary>
    ///     Maps an HTTP PATCH endpoint that accepts a command producing a response of type <typeparamref name="TResponse" />.
    /// </summary>
    /// <typeparam name="TCommand">Command type implementing <see cref="Fluents.Requests.IWitResponse{TResponse}" />.</typeparam>
    /// <typeparam name="TResponse">Response type returned by the command.</typeparam>
    /// <param name="app">The <see cref="RouteGroupBuilder" /> used to register the endpoint.</param>
    /// <param name="endpoint">The URL template for the endpoint.</param>
    /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
    public static RouteHandlerBuilder MapPatch<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
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
    /// <param name="app">The <see cref="RouteGroupBuilder" /> used to register the endpoint.</param>
    /// <param name="endpoint">The URL template for the endpoint.</param>
    /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
    public static RouteHandlerBuilder MapPatch<TCommand>(this RouteGroupBuilder app, string endpoint)
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
    /// </summary>
    /// <typeparam name="TCommand">Command type implementing <see cref="Fluents.Requests.IWitResponse{TResponse}" />.</typeparam>
    /// <typeparam name="TResponse">Response type returned by the command.</typeparam>
    /// <param name="app">The <see cref="RouteGroupBuilder" /> used to register the endpoint.</param>
    /// <param name="endpoint">The URL template for the endpoint.</param>
    /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
    public static RouteHandlerBuilder MapPost<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.IWitResponse<TResponse>
    {
        return app.MapPost(
                endpoint,
                async (IMessageBus bus, TCommand request) =>
                {
                    var rs = await bus.Send(request);
                    return rs.Response(true);
                }).Produces<TResponse>()
            .ProducesCommons();
    }

    /// <summary>
    ///     Maps an HTTP POST endpoint that accepts a command without a response.
    /// </summary>
    /// <typeparam name="TCommand">Command type implementing <see cref="Fluents.Requests.INoResponse" />.</typeparam>
    /// <param name="app">The <see cref="RouteGroupBuilder" /> used to register the endpoint.</param>
    /// <param name="endpoint">The URL template for the endpoint.</param>
    /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
    public static RouteHandlerBuilder MapPost<TCommand>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.INoResponse
    {
        return app.MapPost(
            endpoint,
            async (IMessageBus bus, TCommand request) =>
            {
                var rs = await bus.Send(request);
                return rs.Response(true);
            }).ProducesCommons();
    }

    /// <summary>
    ///     Maps an HTTP PUT endpoint that accepts a command producing a response of type <typeparamref name="TResponse" />.
    /// </summary>
    /// <typeparam name="TCommand">Command type implementing <see cref="Fluents.Requests.IWitResponse{TResponse}" />.</typeparam>
    /// <typeparam name="TResponse">Response type returned by the command.</typeparam>
    /// <param name="app">The <see cref="RouteGroupBuilder" /> used to register the endpoint.</param>
    /// <param name="endpoint">The URL template for the endpoint.</param>
    /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
    public static RouteHandlerBuilder MapPut<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
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
    /// <param name="app">The <see cref="RouteGroupBuilder" /> used to register the endpoint.</param>
    /// <param name="endpoint">The URL template for the endpoint.</param>
    /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
    public static RouteHandlerBuilder MapPut<TCommand>(this RouteGroupBuilder app, string endpoint)
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

    #endregion
}