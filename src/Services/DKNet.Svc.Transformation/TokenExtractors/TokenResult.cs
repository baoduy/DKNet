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
        this.Index = index;
        this.OriginalString = originalString ?? throw new ArgumentNullException(nameof(originalString));
        this.Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        this.Token = token ?? throw new ArgumentNullException(nameof(token));

        if (!this.Definition.IsToken(this.Token))
        {
            throw new InvalidTokenException(token);
        }

        if (index < 0 || index > originalString.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    #endregion

    #region Properties

    public int Index { get; }

    public ITokenDefinition Definition { get; }

    public string Key
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(this._key))
            {
                return this._key;
            }

            this._key = this.Token.Replace(this.Definition.BeginTag, string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(this.Definition.EndTag, string.Empty, StringComparison.OrdinalIgnoreCase);
            return this._key;
        }
    }

    public string OriginalString { get; }

    public string Token { get; }

    #endregion
}