using System.Collections.Concurrent;
using System.Text;
using DKNet.Svc.Transformation.Exceptions;
using DKNet.Svc.Transformation.TokenExtractors;

namespace DKNet.Svc.Transformation;

public interface ITransformerService
{
    /// <summary>
    ///     Transform template from TransformData and additionalData
    /// </summary>
    /// <param name="templateString">the template ex: Hello [Name]. Your {Email} had been [ApprovedStatus]</param>
    /// <param name="parameters">
    ///     the additional Data that is not in the sharing data. the value in additionalData will
    ///     overwrite the value from TransformData as well
    /// </param>
    /// <returns>
    ///     "Hello Duy. Your drunkcoding@outlook.net had been Approved" with TransformData or additionalData is new {Name
    ///     = "Duy", Email= "drunkcoding@outlook.net", ApprovedStatus = "Approved"}
    /// </returns>
    Task<string> TransformAsync(string templateString, params object[] parameters);

    string Transform(string templateString, params object[] parameters);
}

public sealed class TransformerService(TransformOptions options) : ITransformerService
{
    private readonly ConcurrentDictionary<string, object> _cacheService = new(StringComparer.Ordinal);
    private readonly TokenResolver _tokenResolver = new();

    private ITokenExtractor[] GetExtractors() =>
        [.. options.DefaultDefinitions.Select(ITokenExtractor (d) => new TokenExtractor(d))];

    public string Transform(string templateString, params object[] parameters)
    {
        var tokens = GetExtractors().Select(t => t.Extract(templateString));
        return InternalTransform(templateString, tokens.SelectMany(i => i), parameters);
    }

    public async Task<string> TransformAsync(string templateString, params object[] parameters)
    {
        var tokens = await Task.WhenAll(GetExtractors().Select(t => t.ExtractAsync(templateString)));
        return InternalTransform(templateString, tokens.SelectMany(i => i), parameters);
    }

    private string InternalTransform(string template, IEnumerable<IToken> tokens,
        object[] additionalData)
    {
        var builder = new StringBuilder(template);
        var adjustment = 0;

        foreach (var token in tokens.OrderBy(t => t.Index))
        {
            var val =
                TryGetAndCacheValue(token, additionalData)
                ?? throw new UnResolvedTokenException(token);

            var strVal = options.Formatter.Convert(token, val);

            builder = builder.Replace(token.Token, strVal, token.Index + adjustment, token.Token.Length);
            adjustment += strVal.Length - token.Token.Length;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Try to Get data for <see cref="IToken" /> from additionalData and then TransformData and Cache for later use.
    /// </summary>
    /// <param name="token"></param>
    /// <param name="additionalData"></param>
    /// <returns></returns>
    private object TryGetAndCacheValue(IToken token, object[] additionalData)
    {
        return _cacheService.GetOrAdd(token.Token.ToUpperInvariant(),
            _ => TryGetValue(token, additionalData));
    }

    /// <summary>
    ///     Try To Get data for <see cref="IToken" /> from additionalData and then TransformData
    /// </summary>
    /// <param name="token"></param>
    /// <param name="additionalData"></param>
    /// <returns></returns>
    private object TryGetValue(IToken token, object[] additionalData)
    {
        object? val = null;

        if (additionalData.Length > 0)
            val = _tokenResolver.Resolve(token, additionalData);

        if (val is null)
            val = _tokenResolver.Resolve(token, options.GlobalParameters);

        return val!;
    }
}