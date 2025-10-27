namespace DKNet.Svc.Transformation.TokenExtractors;

public interface ITokenExtractor
{
    #region Methods

    /// <summary>
    ///     Extract token from string.
    /// </summary>
    /// <param name="templateString"></param>
    /// <returns></returns>
    IReadOnlyCollection<IToken> Extract(string templateString);

    /// <summary>
    ///     Extract token from string.
    /// </summary>
    /// <param name="templateString"></param>
    /// <returns></returns>
    Task<IReadOnlyCollection<IToken>> ExtractAsync(string templateString);

    #endregion
}

internal sealed class TokenExtractor(ITokenDefinition definition) : ITokenExtractor
{
    #region Properties

    private ITokenDefinition Definition { get; } = definition ?? throw new ArgumentNullException(nameof(definition));

    #endregion

    #region Methods

    public IReadOnlyCollection<IToken> Extract(string templateString) => ExtractCore(templateString);

    public Task<IReadOnlyCollection<IToken>> ExtractAsync(string templateString)
    {
        return Task.Run(() => ExtractCore(templateString));
    }

    private IReadOnlyCollection<IToken> ExtractCore(string templateString)
    {
        if (string.IsNullOrWhiteSpace(templateString)) return [];
        var list = new List<IToken>();

        // Use Span<char> for efficient substring operations
        var span = templateString.AsSpan();
        var beginTag = Definition.BeginTag.AsSpan();
        var endTag = Definition.EndTag.AsSpan();
        var length = span.Length;
        // Find the first occurrence of the begin tag
        var beginIndex = span.IndexOf(beginTag);

        while (beginIndex >= 0 && beginIndex < length)
        {
            // If the next characters are also a begin tag, skip to avoid nested or repeated openers
            if (beginIndex < length - beginTag.Length &&
                span.Slice(beginIndex + beginTag.Length, beginTag.Length)
                    .Equals(beginTag, StringComparison.OrdinalIgnoreCase))
            {
                beginIndex += beginTag.Length;
                continue;
            }

            // Find the end tag after the current begin tag
            var endIndex = span[beginIndex..].IndexOf(endTag);
            if (endIndex < 0) break;
            endIndex += beginIndex; // adjust to absolute position in the span
            if (endIndex <= beginIndex) break;

            // Calculate the full token length (from begin to end tag)
            var tokenLength = endIndex - beginIndex + endTag.Length;
            var tokenSpan = span.Slice(beginIndex, tokenLength);
            var tokenStr = tokenSpan.ToString();

            // Validate the token using the definition
            if (Definition.IsToken(tokenStr))
            {
                // Add the found token to the result list
                list.Add(new TokenResult(Definition, tokenStr, templateString, beginIndex));
                // Move to the next begin tag after the current token
                beginIndex = span[(endIndex + endTag.Length)..].IndexOf(beginTag);
                if (beginIndex >= 0) beginIndex += endIndex + endTag.Length;
            }
            else
            {
                // If not a valid token, search for the next begin tag from the next character
                var nextSearchStart = beginIndex + 1;
                if (nextSearchStart >= length) break;
                var nextBegin = span[nextSearchStart..].IndexOf(beginTag);
                beginIndex = nextBegin >= 0 ? nextBegin + nextSearchStart : -1;
            }
        }

        return list;
    }

    #endregion
}