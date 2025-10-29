using System.Collections.Concurrent;
using System.Text;
using DKNet.Svc.Transformation.Exceptions;
using DKNet.Svc.Transformation.TokenExtractors;
using Microsoft.Extensions.Options;

namespace DKNet.Svc.Transformation;

/// <summary>
///     Interface for TransformerService operations.
/// </summary>
public interface ITransformerService
{
    #region Methods

    string Transform(string templateString, params object[] parameters);

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

    #endregion
}

public sealed class TransformerService(IOptions<TransformOptions> options) : ITransformerService
{
    #region Fields

    private readonly ConcurrentDictionary<string, object> _cacheService = new(StringComparer.Ordinal);
    private readonly TokenResolver _tokenResolver = new();

    #endregion

    #region Properties

    private TransformOptions _options { get; } = options.Value ?? throw new ArgumentNullException(nameof(options));

    #endregion

    #region Methods

    private ITokenExtractor[] GetExtractors() =>
        [.. this._options.DefaultDefinitions.Select(ITokenExtractor (d) => new TokenExtractor(d))];

    private string InternalTransform(string template, IEnumerable<IToken> tokens, object[] additionalData)
    {
        // Sort tokens by index
        var orderedTokens = tokens.OrderBy(t => t.Index).ToArray();
        if (orderedTokens.Length == 0)
        {
            return template;
        }

        var templateSpan = template.AsSpan();
        var builder = new StringBuilder(template.Length);
        var lastIndex = 0;

        foreach (var token in orderedTokens)
        {
            var val = this.TryGetAndCacheValue(token, additionalData) ?? this._options.TokenNotFoundBehavior switch
            {
                TokenNotFoundBehavior.LeaveAsIs => token.Token,
                TokenNotFoundBehavior.Remove => string.Empty,
                TokenNotFoundBehavior.ThrowError => throw new UnResolvedTokenException(token),
                _ => throw new UnResolvedTokenException(token)
            };

            var strVal = this._options.Formatter.Convert(token, val);

            // Append text before the token
            if (token.Index > lastIndex)
            {
                builder.Append(templateSpan[lastIndex..token.Index]);
            }

            // Append replacement value
            builder.Append(strVal);
            lastIndex = token.Index + token.Token.Length;
        }

        // Append any remaining text after the last token
        if (lastIndex < template.Length)
        {
            builder.Append(templateSpan[lastIndex..]);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Transform operation.
    /// </summary>
    public string Transform(string templateString, params object[] parameters)
    {
        var tokens = this.GetExtractors().Select(t => t.Extract(templateString));
        return this.InternalTransform(templateString, tokens.SelectMany(i => i), parameters);
    }

    public async Task<string> TransformAsync(string templateString, params object[] parameters)
    {
        var tokens = await Task.WhenAll(this.GetExtractors().Select(t => t.ExtractAsync(templateString)));
        return this.InternalTransform(templateString, tokens.SelectMany(i => i), parameters);
    }

    /// <summary>
    ///     Try to Get data for <see cref="IToken" /> from additionalData and then TransformData and Cache for later use.
    /// </summary>
    /// <param name="token"></param>
    /// <param name="additionalData"></param>
    /// <returns></returns>
    private object? TryGetAndCacheValue(IToken token, object[] additionalData)
    {
        if (this._cacheService.TryGetValue(token.Token, out var value))
        {
            return value;
        }

        var val = this.TryGetValue(token, additionalData);
        if (val is not null)
        {
            this._cacheService.TryAdd(token.Token, val);
        }

        return val;
    }

    /// <summary>
    ///     Try To Get data for <see cref="IToken" /> from additionalData and then TransformData
    /// </summary>
    /// <param name="token"></param>
    /// <param name="additionalData"></param>
    /// <returns></returns>
    private object? TryGetValue(IToken token, object[] additionalData)
    {
        object? val = null;

        if (additionalData.Length > 0)
        {
            val = this._tokenResolver.Resolve(token, additionalData);
        }

        if (val is null)
        {
            val = this._tokenResolver.Resolve(token, this._options.GlobalParameters);
        }

        return val;
    }

    #endregion
}