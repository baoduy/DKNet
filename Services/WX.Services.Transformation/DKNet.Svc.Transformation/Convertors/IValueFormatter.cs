using DKNet.Svc.Transformation.TokenExtractors;

namespace DKNet.Svc.Transformation.Convertors;

/// <summary>
/// The convertor will be use to convert object to string before replace to the template.
/// </summary>
public interface IValueFormatter
{
    #region Methods

    /// <summary>
    /// Convert Value to String
    /// </summary>
    /// <param name="token">Current Token for reference</param>
    /// <param name="value">The extracted value from data</param>
    /// <returns></returns>
    string Convert(IToken token, object value);

    #endregion Methods
}