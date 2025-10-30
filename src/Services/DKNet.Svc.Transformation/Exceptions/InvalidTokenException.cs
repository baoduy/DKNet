namespace DKNet.Svc.Transformation.Exceptions;

/// <summary>
///     Provides InvalidTokenException functionality.
/// </summary>
/// <param name="token">The token parameter.</param>
/// <param name="innerException">The inner exception parameter.</param>
public sealed class InvalidTokenException(string token, Exception? innerException = null)
    : Exception(token, innerException);