using System.ComponentModel;

namespace DKNet.EfCore.Abstractions.Attributes;

/// <summary>
/// Defines a sequence attribute for generating unique values for fields.
/// </summary>
/// <remarks>
/// The sequence attribute provides functionality to generate sequential values for fields.
/// Supported data types include byte, short, int, and long.
/// Use this attribute to automatically generate sequential values for properties that require unique identifiers.
/// </remarks>
[AttributeUsage(AttributeTargets.Field)]
public sealed class SequenceAttribute : Attribute
{
    private static readonly IReadOnlyCollection<Type> SupportedTypes = new List<Type>
    {
        typeof(byte),
        typeof(short),
        typeof(int),
        typeof(long),
    }.AsReadOnly();

    /// <summary>
    /// Initializes a new instance of the <see cref="SequenceAttribute"/> class.
    /// </summary>
    /// <param name="type">The data type for the sequence. If not specified, defaults to <see cref="int"/>.</param>
    /// <exception cref="NotSupportedException">Thrown when the specified type is not supported by the sequence.</exception>
    public SequenceAttribute(Type? type = null)
    {
        Type = type ?? typeof(int);
        if (!SupportedTypes.Contains(Type))
            throw new NotSupportedException(Type.Name);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the sequence cycles back to minimum after reaching maximum.
    /// </summary>
    /// <value>True if the sequence should cycle; otherwise, false. Defaults to true.</value>
    public bool Cyclic { get; set; } = true;

    /// <summary>
    /// Gets or sets the format string to be applied to the sequence value.
    /// </summary>
    /// <value>The format string pattern, or null if no formatting is needed.</value>
    public string? FormatString { get; set; }

    /// <summary>
    /// Gets or sets the increment step size for the sequence.
    /// </summary>
    /// <value>The number of units to increment by. Defaults to -1.</value>
    [Description("The number of increments for each step")]
    public int IncrementsBy { get; set; } = -1;

    /// <summary>
    /// Gets or sets the maximum value allowed in the sequence.
    /// </summary>
    /// <value>The maximum value. Defaults to -1.</value>
    [Description("The maximum value")]
    public long Max { get; set; } = -1;

    /// <summary>
    /// Gets or sets the minimum value allowed in the sequence.
    /// </summary>
    /// <value>The minimum value. Defaults to -1.</value>
    [Description("The minimum value")]
    [DefaultValue(-1)]
    public long Min { get; set; } = -1;

    /// <summary>
    /// Gets or sets the initial value of the sequence.
    /// </summary>
    /// <value>The starting value. Defaults to -1.</value>
    [Description("The starting value")]
    [DefaultValue(-1)]
    public long StartAt { get; set; } = -1;

    /// <summary>
    /// Gets the data type of the sequence.
    /// </summary>
    /// <value>The Type object representing the sequence's data type.</value>
    [Description("The data type")]
    public Type Type { get; }
}