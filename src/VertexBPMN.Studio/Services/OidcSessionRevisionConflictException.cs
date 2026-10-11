using System.Collections.Concurrent;
using System.Security.Claims;

namespace VertexBPMN.Studio.Services;

/// <summary>Signalisiert eine verlorene Fencing-Wette beim verteilten Session-Schreibzugriff.</summary>
internal sealed class OidcSessionRevisionConflictException(string message) : InvalidOperationException(message);
