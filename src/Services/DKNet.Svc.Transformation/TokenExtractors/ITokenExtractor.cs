namespace DKNet.Svc.Transformation.TokenExtractors;

public interface ITokenExtractor
{
    bool IsToken(string value);

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
}

internal sealed class TokenExtractor(ITokenDefinition definition) : ITokenExtractor
{
    private ITokenDefinition Definition { get; } = definition ?? throw new ArgumentNullException(nameof(definition));

    public bool IsToken(string value) => Definition.IsToken(value);

    public IReadOnlyCollection<IToken> Extract(string templateString) => ExtractCore(templateString);

    public Task<IReadOnlyCollection<IToken>> ExtractAsync(string templateString)
    {
        return Task.Run(() => ExtractCore(templateString));
    }

    private IReadOnlyCollection<IToken> ExtractCore(string templateString)
    {
        if (string.IsNullOrWhiteSpace(templateString)) return [];
        var list = new List<IToken>();

        var span = templateString.AsSpan();
        var beginTag = Definition.BeginTag.AsSpan();
        var endTag = Definition.EndTag.AsSpan();
        var length = span.Length;
        var si = span.IndexOf(beginTag);

        while (si >= 0 && si < length)
        {
            // If next is beginning of Token then move to next
            if (si < length - beginTag.Length &&
                span.Slice(si + beginTag.Length, beginTag.Length).Equals(beginTag, StringComparison.OrdinalIgnoreCase))
            {
                si += beginTag.Length;
                continue;
            }

            var li = span[si..].IndexOf(endTag);
            if (li < 0) break;
            li += si; // adjust to absolute position
            if (li <= si) break;

            var tokenLength = li - si + endTag.Length;
            var tokenSpan = span.Slice(si, tokenLength);
            var tokenStr = tokenSpan.ToString();
            list.Add(new TokenResult(Definition, tokenStr, templateString, si));
            si = span[(li + endTag.Length)..].IndexOf(beginTag);
            if (si >= 0) si += li + endTag.Length; // adjust to absolute position
        }

        return list;
    }
}