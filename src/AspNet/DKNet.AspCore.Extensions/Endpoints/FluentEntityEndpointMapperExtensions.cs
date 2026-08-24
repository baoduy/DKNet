// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: FluentEntityEndpointMapperExtensions.cs
// Description: Extension helpers to map HTTP endpoints directly to entities via the repository/specification pipeline using minimal APIs.

using System.ComponentModel;
using DKNet.AspCore.Extensions.Responses;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DKNet.AspCore.Extensions.Endpoints;

/// <summary>
///     Minimal-API endpoint mapping helpers that wire entity read/delete operations to HTTP verbs through the
///     repository and specification pipeline defined in the EfCore packages.
/// </summary>
public static class FluentsEntityEndpointMapperExtensions
{
    /// <param name="app">The <see cref="RouteGroupBuilder" /> used to register the endpoint.</param>
    extension(RouteGroupBuilder app)
    {
        /// <summary>
        ///     Maps an HTTP GET endpoint that retrieves a single <typeparamref name="TEntity" /> by its <see cref="Guid" />
        ///     id and projects it to <typeparamref name="TModel" />.
        /// </summary>
        /// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" /> with a <see cref="Guid" /> key.</typeparam>
        /// <typeparam name="TModel">Model type the entity is projected to.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapGetById<TEntity, TModel>(string endpoint = "{id}")
            where TEntity : class, IEntity<Guid>
            where TModel : class
        {
            return app.MapGet(
                    endpoint,
                    async (Guid id, [FromServices] IRepositorySpec repo) =>
                    {
                        var model = await repo.FirstOrDefaultAsync(
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
        public RouteHandlerBuilder MapDeleteById<TEntity>(string endpoint = "{id}")
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
        ///     Maps an HTTP GET endpoint that returns a page of <typeparamref name="TEntity" /> records projected to
        ///     <typeparamref name="TModel" />, newest first, optionally filtered and re-ordered by the caller.
        /// </summary>
        /// <remarks>
        ///     Filtering and sorting are restricted to the fields <typeparamref name="TModel" /> already exposes, so
        ///     the endpoint stays generic without widening what a caller can reach. An unusable <c>filter</c> or
        ///     <c>orderBy</c> is answered with <see cref="StatusCodes.Status400BadRequest" /> rather than silently
        ///     ignored, which would answer a filtered query with unfiltered data.
        /// </remarks>
        /// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" /> with a <see cref="Guid" /> key.</typeparam>
        /// <typeparam name="TModel">Model type each entity is projected to.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapGetList<TEntity, TModel>(string endpoint = "/")
            where TEntity : class, IEntity<Guid>
            where TModel : class
        {
            return app.MapGet(
                    endpoint,
                    async (
                        [FromServices] IRepositorySpec repo,
                        [AsParameters] ListQueryRequest request) =>
                    {
                        if (!ListQuery.TryValidate<TEntity, TModel>(request, out var query, out var error))
                            return Results.Problem(error, statusCode: StatusCodes.Status400BadRequest);

                        var page = await repo.ToPagedListAsync(
                            new EntityListSpecification<TEntity, TModel>(query!),
                            request.PageNumberValue,
                            request.PageSizeValue);
                        return Results.Ok(new PagedResponse<TModel>(page));
                    })
                .Produces<PagedResponse<TModel>>()
                .ProducesCommons();
        }
    }
}

/// <summary>
///     Specification matching a single <typeparamref name="TEntity" /> by its <see cref="Guid" /> id, used by
///     <see cref="FluentsEntityEndpointMapperExtensions.MapGetById{TEntity,TModel}" />.
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
///     <see cref="FluentsEntityEndpointMapperExtensions.MapDeleteById{TEntity}" /> to load the tracked entity to delete.
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
///     Specification listing <typeparamref name="TEntity" /> records for
///     <see cref="FluentsEntityEndpointMapperExtensions.MapGetList{TEntity,TModel}" />, applying the caller's
///     validated filter and ordering over a newest-first default.
/// </summary>
/// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" /> with a <see cref="Guid" /> key.</typeparam>
/// <typeparam name="TModel">Model type each entity is projected to.</typeparam>
internal sealed class EntityListSpecification<TEntity, TModel> : ModelSpecification<TEntity, TModel>
    where TEntity : class, IEntity<Guid>
    where TModel : class
{
    /// <summary>
    ///     Initializes the specification from the endpoint's validated query inputs. A caller-supplied
    ///     <see cref="ListQuery{TEntity}.OrderBy" /> replaces the default ordering outright; without one the
    ///     default is newest-first — audited entities order by <c>CreatedOn</c> descending with <c>Id</c> as a
    ///     tie-break, non-audited entities by <c>Id</c> descending alone.
    /// </summary>
    /// <param name="query">The validated filter and ordering inputs.</param>
    public EntityListSpecification(ListQuery<TEntity> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Filter is not null)
            WithFilter(query.Filter);

        if (query.OrderBy is not null)
        {
            AddOrderBy(
                query.OrderBy,
                query.Descending ? ListSortDirection.Descending : ListSortDirection.Ascending);
            return;
        }

        if (typeof(IAuditedEntity<Guid>).IsAssignableFrom(typeof(TEntity)))
            AddOrderBy(nameof(IAuditedProperties.CreatedOn), ListSortDirection.Descending);

        AddOrderByDescending(x => x.Id);
    }
}