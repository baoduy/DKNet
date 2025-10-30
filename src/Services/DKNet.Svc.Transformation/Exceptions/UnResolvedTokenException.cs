using DKNet.Svc.Transformation.TokenExtractors;

namespace DKNet.Svc.Transformation.Exceptions;

/// <summary>
///     Provides UnResolvedTokenException functionality.
/// </summary>
/// <param name="token">The token parameter.</param>
/// <param name="innerException">The inner exception parameter.</param>
public sealed class UnResolvedTokenException(IToken token, Exception? innerException = null)
    : Exception(token.Token, innerException);