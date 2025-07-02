namespace DKNet.Svc.Transformation.TokenExtractors;

public interface ITokenExtractor
{
    #region Methods

    /// <summary>
    /// Extract token from string.
    /// </summary>
    /// <param name="templateString"></param>
    /// <returns></returns>
    IEnumerable<IToken> Extract(string templateString);

    /// <summary>
    /// Extract token from string.
    /// </summary>
    /// <param name="templateString"></param>
    /// <returns></returns>
    Task<IEnumerable<IToken>> ExtractAsync(string templateString);

    #endregion Methods
}