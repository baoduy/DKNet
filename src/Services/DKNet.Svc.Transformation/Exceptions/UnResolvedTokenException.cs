using DKNet.Svc.Transformation.TokenExtractors;

namespace DKNet.Svc.Transformation.Exceptions;

/// <summary>
///     Provides UnResolvedTokenException functionality.
/// </summary>
public sealed class UnResolvedTokenException(IToken token, Exception? innerException = null)
    : Exception(token.Token, innerException);