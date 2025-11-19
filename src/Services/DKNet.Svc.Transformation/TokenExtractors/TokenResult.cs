using System.Diagnostics;
using DKNet.Svc.Transformation.Exceptions;

namespace DKNet.Svc.Transformation.TokenExtractors;

[DebuggerDisplay("Token = {" + nameof(Token) + "}")]
internal sealed class TokenResult : IToken
{
    #region Fields

    private string? _key;

    #endregion

    #region Constructors

    /// <summary>
    ///     The Token result
    /// </summary>
    /// <param name="definition"></param>
    /// <param name="token"></param>
    /// <param name="originalString"></param>
    /// <param name="index"></param>
    public TokenResult(ITokenDefinition definition, string token, string originalString, int index)
    {
        Index = index;
        OriginalString = originalString ?? throw new ArgumentNullException(nameof(originalString));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Token = token ?? throw new ArgumentNullException(nameof(token));

        if (!Definition.IsToken(Token)) throw new InvalidTokenException(token);

        if (index < 0 || index > originalString.Length) throw new ArgumentOutOfRangeException(nameof(index));
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets or sets Definition.
    /// </summary>
    public ITokenDefinition Definition { get; }

    /// <summary>
    ///     Gets or sets Index.
    /// </summary>
    public int Index { get; }

    public string Key
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_key)) return _key;

            _key = Token.Replace(Definition.BeginTag, string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(Definition.EndTag, string.Empty, StringComparison.OrdinalIgnoreCase);
            return _key;
        }
    }

    /// <summary>
    ///     Gets or sets OriginalString.
    /// </summary>
    public string OriginalString { get; }

    /// <summary>
    ///     Gets or sets Token.
    /// </summary>
    public string Token { get; }

    #endregion
}