using System.Collections.Concurrent;
using System.Text;
using DKNet.Svc.Transformation.Exceptions;
using DKNet.Svc.Transformation.TokenExtractors;

namespace DKNet.Svc.Transformation;

public sealed class TransformerService(TransformOptions options) : ITransformerService
{
    private readonly ConcurrentDictionary<string, object> _cacheService = new(StringComparer.Ordinal);

    public async Task<string> TransformAsync(string templateString, params object[] parameters)
    {
        var tokens = await Task.WhenAll(options.TokenExtractors.Select(t => t.ExtractAsync(templateString)))
            ;
        return await InternalTransformAsync(templateString, tokens.SelectMany(i => i), parameters)
            ;
    }

    public async Task<string> TransformAsync(string templateString, Func<IToken, Task<object>> tokenFactory)
    {
        var tokens = await Task.WhenAll(options.TokenExtractors.Select(t => t.ExtractAsync(templateString)));
        return await InternalTransformAsync(templateString, tokens.SelectMany(i => i), [], tokenFactory);
    }


    private async Task<string> InternalTransformAsync(string template, IEnumerable<IToken> tokens,
        object[] additionalData, Func<IToken, Task<object>>? dataProvider = null)
    {
        var builder = new StringBuilder(template);
        var adjustment = 0;

        foreach (var token in tokens.OrderBy(t => t.Index))
        {
            var val = (await TryGetAndCacheValueAsync(token, dataProvider) ??
                       TryGetAndCacheValue(token, additionalData))
                      ?? throw new UnResolvedTokenException(token);

            var strVal = options.Formatter.Convert(token, val);

            builder = builder.Replace(token.Token, strVal, token.Index + adjustment, token.Token.Length);
            adjustment += strVal.Length - token.Token.Length;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Try to Get data for <see cref="IToken"/> from additionalData and then TransformData and Cache for later use.
    /// </summary>
    /// <param name="token"></param>
    /// <param name="additionalData"></param>
    /// <returns></returns>
    private object TryGetAndCacheValue(IToken token, object[] additionalData) =>
        options.DisabledLocalCache
            ? TryGetValue(token, additionalData)
            : _cacheService.GetOrAdd(token.Token.ToUpperInvariant(),
                _ => TryGetValue(token, additionalData));

    /// <summary>
    /// Try to Get data for <see cref="IToken"/> from dataProvider and Cache for later use.
    /// </summary>
    /// <param name="token"></param>
    /// <param name="dataProvider"></param>
    /// <returns></returns>
    private async Task<object?> TryGetAndCacheValueAsync(IToken token,
        Func<IToken, Task<object>>? dataProvider)
    {
        if (dataProvider == null) return null;
        var val = await dataProvider(token);
        return options.DisabledLocalCache
            ? val
            : _cacheService.GetOrAdd(token.Token.ToUpperInvariant(), _ => val);
    }

    /// <summary>
    /// Try Get data for <see cref="IToken"/> from additionalData and then TransformData
    /// </summary>
    /// <param name="token"></param>
    /// <param name="additionalData"></param>
    /// <returns></returns>
    private object TryGetValue(IToken token, object[] additionalData)
    {
        object? val = null;

        if (additionalData.Length > 0)
            val = options.TokenResolver.Resolve(token, additionalData);

        if (val is null)
            val = options.TokenResolver.Resolve(token, options.GlobalParameters);

        return val!;
    }

    internal void ClearCache() => _cacheService.Clear();
}