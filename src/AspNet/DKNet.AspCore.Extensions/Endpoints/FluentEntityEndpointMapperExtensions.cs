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
/// <remarks>
///     Each mapper comes in two shapes: one carrying an explicit <c>TKey</c> for entities keyed by anything
///     (<see cref="int" />, <see cref="string" />, a strongly-typed id), and a <see cref="Guid" /> shorthand that
///     forwards to it. <c>TKey</c> sits immediately after <c>TEntity</c> so an entity and its key type read
///     together, ahead of the projection the caller receives. <c>TKey</c> is constrained to
///     <see cref="IEquatable{T}" /> rather than <see cref="IParsable{TSelf}" /> deliberately: the looser
///     constraint keeps <see cref="string" /> keys usable (<see cref="string" /> implements the former but not the
///     latter) and minimal APIs bind those natively. The cost is that a key type the framework cannot bind fails
///     when the route is built rather than at compile time.
/// </remarks>
public static class FluentsEntityEndpointMapperExtensions
{
    /// <param name="app">The <see cref="RouteGroupBuilder" /> used to register the endpoint.</param>
    extension(RouteGroupBuilder app)
    {
        /// <summary>
        ///     Maps an HTTP GET endpoint that retrieves a single <typeparamref name="TEntity" /> by its
        ///     <typeparamref name="TKey" /> id and projects it to <typeparamref name="TModel" />.
        /// </summary>
        /// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" />.</typeparam>
        /// <typeparam name="TKey">The entity's primary key type.</typeparam>
        /// <typeparam name="TModel">Model type the entity is projected to.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapGetById<TEntity, TKey, TModel>(string endpoint = "{id}")
            where TEntity : class, IEntity<TKey>
            where TKey : IEquatable<TKey>
            where TModel : class
        {
            return app.MapGet(
                    endpoint,
                    async (TKey id, [FromServices] IRepositorySpec repo) =>
                    {
                        var model = await repo.FirstOrDefaultAsync(
                            new EntityByIdSpecification<TEntity, TKey, TModel>(id));
                        return model is null ? Results.NotFound() : Results.Ok(model);
                    })
                .Produces<TModel>()
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps an HTTP GET endpoint that retrieves a single <see cref="Guid" />-keyed
        ///     <typeparamref name="TEntity" /> by its id and projects it to <typeparamref name="TModel" />.
        /// </summary>
        /// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" /> with a <see cref="Guid" /> key.</typeparam>
        /// <typeparam name="TModel">Model type the entity is projected to.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapGetById<TEntity, TModel>(string endpoint = "{id}")
            where TEntity : class, IEntity<Guid>
            where TModel : class
            => app.MapGetById<TEntity, Guid, TModel>(endpoint);

        /// <summary>
        ///     Maps an HTTP DELETE endpoint that hard-deletes a single <typeparamref name="TEntity" /> by its
        ///     <typeparamref name="TKey" /> id, going through the repository's save pipeline so audit-log and
        ///     domain-event hooks fire as for any other removal.
        /// </summary>
        /// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" />.</typeparam>
        /// <typeparam name="TKey">The entity's primary key type.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapDeleteById<TEntity, TKey>(string endpoint = "{id}")
            where TEntity : class, IEntity<TKey>
            where TKey : IEquatable<TKey>
        {
            return app.MapDelete(
                    endpoint,
                    async (TKey id, [FromServices] IRepositorySpec repo, CancellationToken cancellationToken) =>
                    {
                        var entity = await repo.FirstOrDefaultAsync(
                            new EntityByIdSpecification<TEntity, TKey>(id), cancellationToken);
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
        ///     Maps an HTTP DELETE endpoint that hard-deletes a single <see cref="Guid" />-keyed
        ///     <typeparamref name="TEntity" /> by its id.
        /// </summary>
        /// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" /> with a <see cref="Guid" /> key.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapDeleteById<TEntity>(string endpoint = "{id}")
            where TEntity : class, IEntity<Guid>
            => app.MapDeleteById<TEntity, Guid>(endpoint);

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
        /// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" />.</typeparam>
        /// <typeparam name="TKey">The entity's primary key type.</typeparam>
        /// <typeparam name="TModel">Model type each entity is projected to.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapGetList<TEntity, TKey, TModel>(string endpoint = "/")
            where TEntity : class, IEntity<TKey>
            where TKey : IEquatable<TKey>
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
                            new EntityListSpecification<TEntity, TKey, TModel>(query!),
                            request.PageNumberValue,
                            request.PageSizeValue);
                        return Results.Ok(new PagedResponse<TModel>(page));
                    })
                .Produces<PagedResponse<TModel>>()
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps an HTTP GET endpoint that returns a page of <see cref="Guid" />-keyed
        ///     <typeparamref name="TEntity" /> records projected to <typeparamref name="TModel" />.
        /// </summary>
        /// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" /> with a <see cref="Guid" /> key.</typeparam>
        /// <typeparam name="TModel">Model type each entity is projected to.</typeparam>
        /// <param name="endpoint">The URL template for the endpoint.</param>
        /// <returns>A configured <see cref="RouteHandlerBuilder" />.</returns>
        public RouteHandlerBuilder MapGetList<TEntity, TModel>(string endpoint = "/")
            where TEntity : class, IEntity<Guid>
            where TModel : class
            => app.MapGetList<TEntity, Guid, TModel>(endpoint);
    }
}

/// <summary>
///     Specification matching a single <typeparamref name="TEntity" /> by its <typeparamref name="TKey" /> id, used
///     by <see cref="FluentsEntityEndpointMapperExtensions.MapGetById{TEntity,TKey,TModel}" />.
/// </summary>
/// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" />.</typeparam>
/// <typeparam name="TKey">The entity's primary key type.</typeparam>
/// <typeparam name="TModel">Model type the entity is projected to.</typeparam>
internal sealed class EntityByIdSpecification<TEntity, TKey, TModel> : ModelSpecification<TEntity, TModel>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>
    where TModel : class
{
    /// <summary>Initializes the specification with a filter matching the given id.</summary>
    /// <param name="id">The entity id to match.</param>
    public EntityByIdSpecification(TKey id) => WithFilter(x => x.Id.Equals(id));
}

/// <summary>
///     Specification matching a single <typeparamref name="TEntity" /> by its <typeparamref name="TKey" /> id, used
///     by <see cref="FluentsEntityEndpointMapperExtensions.MapDeleteById{TEntity,TKey}" /> to load the tracked
///     entity to delete.
/// </summary>
/// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" />.</typeparam>
/// <typeparam name="TKey">The entity's primary key type.</typeparam>
internal sealed class EntityByIdSpecification<TEntity, TKey> : Specification<TEntity>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Initializes the specification with a filter matching the given id.</summary>
    /// <param name="id">The entity id to match.</param>
    // IEquatable<TKey>.Equals rather than ==, which no generic type parameter supports. EF Core translates it to
    // the same parameterized `Id = @id` comparison a Guid == Guid produced, for value and reference keys alike.
    public EntityByIdSpecification(TKey id) => WithFilter(x => x.Id.Equals(id));
}

/// <summary>
///     Specification listing <typeparamref name="TEntity" /> records for
///     <see cref="FluentsEntityEndpointMapperExtensions.MapGetList{TEntity,TKey,TModel}" />, applying the caller's
///     validated filter and ordering over a newest-first default.
/// </summary>
/// <typeparam name="TEntity">Entity type implementing <see cref="IEntity{TKey}" />.</typeparam>
/// <typeparam name="TKey">The entity's primary key type.</typeparam>
/// <typeparam name="TModel">Model type each entity is projected to.</typeparam>
internal sealed class EntityListSpecification<TEntity, TKey, TModel> : ModelSpecification<TEntity, TModel>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>
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

            // A caller-chosen field is rarely unique, and rows tied on it have no defined order — paging over
            // them repeats and drops rows on a real database. Appending the unique key pins the order; skipped
            // when the caller already ordered by Id, where a second Id key would be dead weight.
            if (!string.Equals(query.OrderBy, nameof(IEntity<TKey>.Id), StringComparison.Ordinal))
                AddOrderByDescending(x => x.Id);
            return;
        }

        if (typeof(IAuditedEntity<TKey>).IsAssignableFrom(typeof(TEntity)))
            AddOrderBy(nameof(IAuditedProperties.CreatedOn), ListSortDirection.Descending);

        AddOrderByDescending(x => x.Id);
    }
}
