namespace DKNet.Svc.Transformation.Exceptions;

public sealed class InvalidTokenException(string token, Exception? innerException = null) : Exception(token, innerException);