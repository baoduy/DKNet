using System.Linq.Expressions;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     The specification expression visitor that replaces parameter expressions in an expression tree.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="ReplaceExpressionVisitor"/> class.
/// </remarks>
/// <param name="oldValue">Old expression to be replaced</param>
/// <param name="newValue">A new expression that replaces the old one</param>
internal class ReplaceExpressionVisitor(Expression oldValue, Expression newValue) : ExpressionVisitor
{
    private readonly Expression _newValue = newValue;
    private readonly Expression _oldValue = oldValue;

    /// <summary>
    ///     Visits the expression and replaces the old parameter with the new one.
    /// </summary>
    /// <param name="node">The expression node to visit</param>
    /// <returns>The modified expression</returns>
    public override Expression Visit(Expression? node)
    {
        return (node == _oldValue ? _newValue : base.Visit(node))!;
    }
}