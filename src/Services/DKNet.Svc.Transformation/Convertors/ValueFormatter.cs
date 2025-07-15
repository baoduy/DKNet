using System.Globalization;
using DKNet.Svc.Transformation.TokenExtractors;

namespace DKNet.Svc.Transformation.Convertors;

/// <summary>
///     The convertor will be used to convert object to string before replace to the template.
/// </summary>
public sealed class ValueFormatter : IValueFormatter
{
    public string DateFormat { get; set; } = "dd/MM/yyyy hh.mm.ss";

    public string IntegerFormat { get; set; } = "###,##0";

    public string NumberFormat { get; set; } = "###,##0.00";

    public string Convert(IToken token, object? value)
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
}