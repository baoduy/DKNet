using System.Linq.Expressions;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     The specification expression visitor that replaces parameter expression
/// </summary>
/// <remarks>
///     Initializes a new instance of the class
/// </remarks>
/// <param name="oldValue">Old expression to be replaced</param>
/// <param name="newValue">A new expression that replaces the old one</param>
internal class ReplaceExpressionVisitor(Expression oldValue, Expression newValue) : ExpressionVisitor
{
    private readonly Expression _newValue = newValue;
    private readonly Expression _oldValue = oldValue;

    public override Expression Visit(Expression? node)
    {
        return (node == _oldValue ? _newValue : base.Visit(node))!;
    }
}