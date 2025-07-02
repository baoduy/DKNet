using DKNet.Svc.Transformation.TokenExtractors;

namespace DKNet.Svc.Transformation.Exceptions;

public sealed class UnResolvedTokenException(IToken token, Exception? innerException = null) : Exception(token.Token, innerException);