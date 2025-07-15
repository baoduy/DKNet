namespace DKNet.EfCore.Abstractions.Attributes;

/// <summary>
///     Initializes a new instance of the <see cref="SqlSequenceAttribute" /> class.
///     This attribute is used to specify the schema name for an SQL sequence associated with an enum.
/// </summary>
/// <param name="schema">The schema name for the SQL sequence.</param>
[AttributeUsage(AttributeTargets.Enum)]
public sealed class SqlSequenceAttribute(string schema = "seq") : Attribute
{
    /// <summary>
    ///     Gets or sets the schema name for the SQL sequence.
    ///     The schema name defaults to "seq" if not specified.
    /// </summary>
    public string Schema { get; } = schema;
}