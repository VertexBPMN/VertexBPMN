namespace VertexBPMN.Studio.Services;

public sealed class OidcSessionExpiredException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
