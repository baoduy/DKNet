// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: Repository.cs
// Description: Read/write repository implementation that composes write functionality and exposes read helpers.

namespace DKNet.EfCore.Repos;

/// <summary>
///     Default repository implementation that provides read helpers on top of <see cref="WriteRepository{TEntity}" />.
/// </summary>
/// <typeparam name="TEntity">Entity CLR type.</typeparam>
[Obsolete("DKNet.EfCore.Repos is retired. Use DKNet.EfCore.Specifications (IRepositorySpec + SpecSetup) instead. See docs/EfCore/Migrating-Repos-To-Specifications.md.")]
#pragma warning disable CS0618 // base type/interface are the obsolete members being flagged here
public sealed class Repository<TEntity>
    : WriteRepository<TEntity>, IRepository<TEntity>
    where TEntity : class
{
    #region Fields

    private readonly DbContext _dbContext;
    private readonly IMapper? _mapper;

    #endregion

    #region Constructors

    /// <inheritdoc />
    public Repository(DbContext dbContext, IServiceProvider? provider = null) : base(dbContext, provider)
    {
        _dbContext = dbContext;
        _mapper = provider?.GetService(typeof(IMapper)) as IMapper;
    }

    /// <inheritdoc />
    internal Repository(DbContext dbContext, IMapper? mapper = null) : base(dbContext)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    #endregion

    #region Methods

    /// <summary>
    ///     Returns a TRACKED queryable for the entity type so that materialized entities can be modified and persisted.
    /// </summary>
    /// <remarks>
    ///     <see cref="ReadRepository{TEntity}.Query()" /> returns <c>AsNoTracking()</c> for read-only scenarios;
    ///     this override deliberately returns a tracked queryable for read-then-update workflows.
    /// </remarks>
    public override IQueryable<TEntity> Query() => _dbContext.Set<TEntity>();

    /// <summary>
    ///     Projects entities that satisfy the specified <paramref name="filter" /> to the target model
    ///     <typeparamref name="TModel" />.
    ///     Requires an <see cref="IMapper" /> to be registered.
    /// </summary>
    /// <typeparam name="TModel">The model type to project to.</typeparam>
    /// <param name="filter">Predicate to filter entities.</param>
    /// <returns>An <see cref="IQueryable{TModel}" /> representing the projected query.</returns>
    public override IQueryable<TModel> Query<TModel>(Expression<Func<TEntity, bool>> filter)
        where TModel : class
    {
        if (_mapper is null) throw new InvalidOperationException("IMapper is not registered.");

        var query = Query(filter);
        return query.ProjectToType<TModel>(_mapper.Config);
    }

    #endregion
}
#pragma warning restore CS0618
