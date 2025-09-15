﻿using System.Linq.Expressions;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     Represents an expression that combines two specifications with a logical "or"
/// </summary>
/// <typeparam name="TEntity">Type of the entity</typeparam>
public class OrSpecification<TEntity> : Specification<TEntity>
    where TEntity : class
{
    /// <summary>
    ///     Initializes a new instance of the class
    /// </summary>
    /// <param name="left">A left operand expression to combine to</param>
    /// <param name="right">A right operand expression to combine with</param>
    public OrSpecification(Specification<TEntity> left, Specification<TEntity> right)
    {
        RegisterFilteringQuery(left, right);
    }

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