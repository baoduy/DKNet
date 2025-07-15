using DKNet.Svc.Transformation.TokenDefinitions;

// ReSharper disable MemberCanBePrivate.Global

namespace DKNet.Svc.Transformation.TokenExtractors;

/// <summary>
///     The extractor of &lt;token&gt;
/// </summary>
public class AngledBracketTokenExtractor() : TokenExtractor(new AngledBracketDefinition());

/// <summary>
///     The extractor of {token}
/// </summary>
public class CurlyBracketExtractor() : TokenExtractor(new CurlyBracketDefinition());

/// <summary>
///     The extractor of [token]
/// </summary>
public class SquareBracketExtractor() : TokenExtractor(new SquareBracketDefinition());

public class TokenExtractor(ITokenDefinition definition) : ITokenExtractor
{
    protected ITokenDefinition Definition { get; } = definition ?? throw new ArgumentNullException(nameof(definition));

    public IEnumerable<IToken> Extract(string templateString) => ExtractCore(templateString);

    public Task<IEnumerable<IToken>> ExtractAsync(string templateString)
    {
        return Task.Run(() => ExtractCore(templateString));
    }

    protected virtual IEnumerable<IToken> ExtractCore(string templateString)
    {
        if (string.IsNullOrWhiteSpace(templateString)) yield break;

        var length = templateString.Length;
        var si = templateString.IndexOf(Definition.BeginTag, StringComparison.Ordinal);

        while (si >= 0 && si < length)
        {
            //If next is beginning of Token then move to next
            if (si < length - 1 &&
                string.Equals(templateString.Substring(si + Definition.BeginTag.Length, Definition.BeginTag.Length),
                    Definition.BeginTag, StringComparison.OrdinalIgnoreCase))
            {
                si += Definition.BeginTag.Length;
                continue;
            }

            var li = templateString.IndexOf(Definition.EndTag, si, StringComparison.Ordinal);
            if (li <= si) break;

            yield return new TokenResult(Definition, templateString.Substring(si, li - si + Definition.BeginTag.Length),
                templateString, si);
            si = templateString.IndexOf(Definition.BeginTag, li, StringComparison.Ordinal);
        }
    }
}