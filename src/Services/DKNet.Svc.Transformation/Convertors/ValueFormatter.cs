using System.Globalization;
using DKNet.Svc.Transformation.TokenExtractors;

// ReSharper disable ClassWithVirtualMembersNeverInherited.Global

namespace DKNet.Svc.Transformation.Convertors;

/// <summary>
///     The convertor will be used to convert object to string before replace to the template.
/// </summary>
/// <summary>
///     Provides ValueFormatter functionality.
/// </summary>
public class ValueFormatter : IValueFormatter
{
    #region Properties

    /// <summary>
    /// </summary>
    public virtual string DateFormat { get; set; } = "dd/MM/yyyy hh.mm.ss";

    /// <summary>
    /// </summary>
    public virtual string IntegerFormat { get; set; } = "###,##0";

    /// <summary>
    /// </summary>
    public virtual string NumberFormat { get; set; } = "###,##0.00";

    #endregion

    #region Methods

    /// <summary>
    ///     Convert operation.
    /// </summary>
    /// <param name="token">The token parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <returns>The result of the operation.</returns>
    public virtual string Convert(IToken token, object? value)
    {
        return value == null
            ? string.Empty
            : value switch
            {
                bool b => b ? "Yes" : "No",
                int a => a.ToString(IntegerFormat, CultureInfo.InvariantCulture),
                long a => a.ToString(IntegerFormat, CultureInfo.InvariantCulture),
                double a => a.ToString(NumberFormat, CultureInfo.InvariantCulture),
                decimal a => a.ToString(NumberFormat, CultureInfo.InvariantCulture),
                float a => a.ToString(NumberFormat, CultureInfo.InvariantCulture),
                DateTime a => a.ToString(DateFormat, CultureInfo.InvariantCulture),
                DateTimeOffset a => a.ToString(DateFormat, CultureInfo.InvariantCulture),
                _ => value.ToString()!
            };
    }

    #endregion
}