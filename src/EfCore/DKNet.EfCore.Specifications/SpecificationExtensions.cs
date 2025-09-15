using DKNet.EfCore.Extensions.Extensions;
using DKNet.EfCore.Repos.Abstractions;
using Microsoft.EntityFrameworkCore;
using X.PagedList;
using X.PagedList.EF;

namespace DKNet.EfCore.Specifications;

public static class SpecificationExtensions
{
    private static IQueryable<TEntity> ApplySpecs<TEntity>(
        this IQueryable<TEntity> query,
        ISpecification<TEntity> specification) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(specification, nameof(specification));
        var efCoreSpecification = new EfCoreSpecification<TEntity>(specification);
        return efCoreSpecification.Apply(query);
    }

    // public static IQueryable<TEntity> ApplySpecs<TEntity>(
    //     this DbContext dbContext,
    //     ISpecification<TEntity> specification) where TEntity : class
    // {
    //     return dbContext.Set<TEntity>().AsQueryable().ApplySpecs(specification);
    // }

    public static IQueryable<TEntity> ApplySpecs<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification) where TEntity : class
    {
        return repo.Gets().ApplySpecs(specification);
    }

    public static Task<List<TEntity>> SpecsListAsync<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification, CancellationToken cancellationToken = default) where TEntity : class
    {
        return repo.ApplySpecs(specification).ToListAsync(cancellationToken);
    }

    public static Task<TEntity?> SpecsFirstOrDefaultAsync<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification, CancellationToken cancellationToken = default) where TEntity : class
    {
        return repo.ApplySpecs(specification).FirstOrDefaultAsync(cancellationToken);
    }

    public static IAsyncEnumerable<TEntity> SpecsToPageEnumerable<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification) where TEntity : class
    {
        return repo.ApplySpecs(specification).ToPageEnumerable();
    }

    public static Task<IPagedList<TEntity>> SpecsToPageListAsync<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification, int pageNumber,
        int pageSize) where TEntity : class
    {
        return repo.ApplySpecs(specification)
            .ToPagedListAsync(pageNumber, pageSize);
    }
}