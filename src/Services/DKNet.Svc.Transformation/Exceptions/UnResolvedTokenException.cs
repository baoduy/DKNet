using DKNet.Svc.Transformation.TokenExtractors;

namespace DKNet.Svc.Transformation.Exceptions;

/// <summary>
/// <summary>
///     Provides UnResolvedTokenException functionality.
/// </summary>
/// <param name="token">The token parameter.</param>
/// <param name="null">The null parameter.</param>
public sealed class UnResolvedTokenException(IToken token, Exception? innerException = null)
    : Exception(token.Token, innerException);