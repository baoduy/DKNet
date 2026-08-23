// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: FluentEndpointMapperExtensions.cs
// Description: Extension helpers to map HTTP endpoints to SlimMessageBus-based fluent requests/queries using minimal APIs.

using System.ComponentModel;
using DKNet.AspCore.Extensions.Responses;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using DKNet.SlimBus.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SlimMessageBus;

namespace DKNet.AspCore.Extensions.Endpoints;

/// <summary>
///     Minimal-API endpoint mapping helpers that wire SlimMessageBus requests/queries to HTTP verbs using
///     the fluent request/query interfaces defined in the SlimBus package.
/// </summary>
public static class FluentsEndpointMapperExtensions
{
    #region Methods

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
        ///     Maps an HTTP GET endpoint that retrieves a single <typeparamref name="TEntity" /> by its <see cref="Guid" />
        ///     id and projects it to <typeparamref name="TModel" />.
        /// </summary>
        /// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" /> with a <see cref="Guid" /> key.</typeparam>
        /// <typeparam name="TModel">Model type the entity is projected to.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapGetById<TEntity, TModel>(string endpoint)
            where TEntity : class, IEntity<Guid>
            where TModel : class
        {
            return app.MapGet(
                    endpoint,
                    async (Guid id, [FromServices] IRepositorySpec repo) =>
                    {
                        var model = await repo.FirstOrDefaultAsync<TEntity, TModel>(
                            new EntityByIdSpecification<TEntity, TModel>(id));
                        return model is null ? Results.NotFound() : Results.Ok(model);
                    })
                .Produces<TModel>()
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps an HTTP DELETE endpoint that hard-deletes a single <typeparamref name="TEntity" /> by its
        ///     <see cref="Guid" /> id, going through the repository's save pipeline so audit-log and domain-event
        ///     hooks fire as for any other removal.
        /// </summary>
        /// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" /> with a <see cref="Guid" /> key.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapDeleteById<TEntity>(string endpoint)
            where TEntity : class, IEntity<Guid>
        {
            return app.MapDelete(
                    endpoint,
                    async (Guid id, [FromServices] IRepositorySpec repo, CancellationToken cancellationToken) =>
                    {
                        var entity = await repo.FirstOrDefaultAsync(
                            new EntityByIdSpecification<TEntity>(id), cancellationToken);
                        if (entity is null) return Results.NotFound();

                        repo.Delete(entity);
                        try
                        {
                            await repo.SaveChangesAsync(cancellationToken);
                        }
                        catch (DbUpdateException)
                        {
                            return Results.Conflict();
                        }

                        return Results.NoContent();
                    })
                .Produces(StatusCodes.Status204NoContent)
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps an HTTP GET endpoint that returns a page of <typeparamref name="TEntity" /> records, newest first,
        ///     projected to <typeparamref name="TModel" />.
        /// </summary>
        /// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" /> with a <see cref="Guid" /> key.</typeparam>
        /// <typeparam name="TModel">Model type each entity is projected to.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapGetList<TEntity, TModel>(string endpoint)
            where TEntity : class, IEntity<Guid>
            where TModel : class
        {
            return app.MapGet(
                    endpoint,
                    async (
                        [FromServices] IRepositorySpec repo,
                        int pageNumber = 1,
                        [Description("Number of items per page. Values above 100 are clamped to 100.")]
                        int pageSize = 20) =>
                    {
                        var page = await repo.ToPagedListAsync<TEntity, TModel>(
                            new EntityListSpecification<TEntity, TModel>(),
                            pageNumber < 1 ? 1 : pageNumber,
                            pageSize < 1 ? 20 : Math.Min(pageSize, 100));
                        return Results.Ok(new PagedResponse<TModel>(page));
                    })
                .Produces<PagedResponse<TModel>>()
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
    }
}

/// <summary>
///     Specification matching a single <typeparamref name="TEntity" /> by its <see cref="Guid" /> id, used by
///     <see cref="FluentsEndpointMapperExtensions.MapGetById{TEntity,TModel}" />.
/// </summary>
/// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" /> with a <see cref="Guid" /> key.</typeparam>
/// <typeparam name="TModel">Model type the entity is projected to.</typeparam>
internal sealed class EntityByIdSpecification<TEntity, TModel> : ModelSpecification<TEntity, TModel>
    where TEntity : class, IEntity<Guid>
    where TModel : class
{
    /// <summary>Initializes the specification with a filter matching the given id.</summary>
    /// <param name="id">The entity id to match.</param>
    public EntityByIdSpecification(Guid id) => WithFilter(x => x.Id == id);
}

/// <summary>
///     Specification matching a single <typeparamref name="TEntity" /> by its <see cref="Guid" /> id, used by
///     <see cref="FluentsEndpointMapperExtensions.MapDeleteById{TEntity}" /> to load the tracked entity to delete.
/// </summary>
/// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" /> with a <see cref="Guid" /> key.</typeparam>
internal sealed class EntityByIdSpecification<TEntity> : Specification<TEntity>
    where TEntity : class, IEntity<Guid>
{
    /// <summary>Initializes the specification with a filter matching the given id.</summary>
    /// <param name="id">The entity id to match.</param>
    public EntityByIdSpecification(Guid id) => WithFilter(x => x.Id == id);
}

/// <summary>
///     Specification listing <typeparamref name="TEntity" /> records ordered newest-first, used by
///     <see cref="FluentsEndpointMapperExtensions.MapGetList{TEntity,TModel}" />.
/// </summary>
/// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" /> with a <see cref="Guid" /> key.</typeparam>
/// <typeparam name="TModel">Model type each entity is projected to.</typeparam>
internal sealed class EntityListSpecification<TEntity, TModel> : ModelSpecification<TEntity, TModel>
    where TEntity : class, IEntity<Guid>
    where TModel : class
{
    /// <summary>
    ///     Initializes the specification with the default newest-first ordering: audited entities order by
    ///     <c>CreatedOn</c> descending with <c>Id</c> as a tie-break; non-audited entities order by <c>Id</c>
    ///     descending alone.
    /// </summary>
    public EntityListSpecification()
    {
        if (typeof(IAuditedEntity<Guid>).IsAssignableFrom(typeof(TEntity)))
            AddOrderBy(nameof(IAuditedProperties.CreatedOn), ListSortDirection.Descending);

        AddOrderByDescending(x => x.Id);
    }
}