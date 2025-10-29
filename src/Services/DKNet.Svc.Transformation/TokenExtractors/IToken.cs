namespace DKNet.Svc.Transformation.TokenExtractors;

/// <summary>
///     Interface for Token operations.
/// </summary>
public interface IToken
{
    #region Properties

    /// <summary>
    ///     The start index of token in the original string.
    /// </summary>
    int Index { get; }

    /// <summary>
    ///     The token definition
    /// </summary>
    ITokenDefinition Definition { get; }

    /// <summary>
    ///     The key only of token.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// </summary>
    string OriginalString { get; }

    /// <summary>
    ///     The token value. Ex: [key]
    /// </summary>
    string Token { get; }

    #endregion
}