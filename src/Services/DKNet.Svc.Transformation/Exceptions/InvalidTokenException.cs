namespace DKNet.Svc.Transformation.Exceptions;

/// <summary>
///     Provides InvalidTokenException functionality.
/// </summary>
public sealed class InvalidTokenException(string token, Exception? innerException = null)
    : Exception(token, innerException);