using System.Linq.Expressions;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     Represents an expression that combines two specifications with a logical "or".
/// </summary>
/// <typeparam name="TEntity">Type of the entity</typeparam>
public class OrSpecification<TEntity> : Specification<TEntity>
    where TEntity : class
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="OrSpecification{TEntity}"/> class.
    /// </summary>
    /// <param name="left">A left operand specification to combine</param>
    /// <param name="right">A right operand specification to combine</param>
    public OrSpecification(Specification<TEntity> left, Specification<TEntity> right)
    {
        RegisterFilteringQuery(left, right);
    }

    /// <summary>
    ///     Registers the filtering query by combining two specifications with logical "or".
    /// </summary>
    /// <param name="left">Left specification</param>
    /// <param name="right">Right specification</param>
    private void RegisterFilteringQuery(Specification<TEntity> left, Specification<TEntity> right)
    {
        var leftExpression = left.FilterQuery;
        var rightExpression = right.FilterQuery;

        if (leftExpression is null && rightExpression is null) return;

        if (leftExpression is not null && rightExpression is null)
        {
            WithFilter(leftExpression);
            return;
        }

        if (leftExpression is null && rightExpression is not null)
        {
            WithFilter(rightExpression);
            return;
        }

        var replaceVisitor =
            new ReplaceExpressionVisitor(rightExpression!.Parameters.Single(), leftExpression!.Parameters.Single());
        var replacedBody = replaceVisitor.Visit(rightExpression.Body);

        var andExpression = Expression.OrElse(leftExpression.Body, replacedBody);
        var lambda = Expression.Lambda<Func<TEntity, bool>>(andExpression, leftExpression.Parameters.Single());

        WithFilter(lambda);
    }
}