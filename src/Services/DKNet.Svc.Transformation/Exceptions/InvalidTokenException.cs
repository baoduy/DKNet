namespace DKNet.Svc.Transformation.Exceptions;

/// <summary>
/// <summary>
///     Provides InvalidTokenException functionality.
/// </summary>
/// <param name="token">The token parameter.</param>
/// <param name="null">The null parameter.</param>
public sealed class InvalidTokenException(string token, Exception? innerException = null)
    : Exception(token, innerException);