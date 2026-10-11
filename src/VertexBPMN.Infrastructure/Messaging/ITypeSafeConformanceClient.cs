namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>Schmaler HTTP-Client für die TypeSafe System One Evaluierung (POST /v1/systemone).</summary>
public interface ITypeSafeConformanceClient
{
    /// <summary>
    /// Fragt ein Choice-Urteil zur Envelope-Konformität ab. Gibt null zurück, wenn die Antwort nicht
    /// als gültige Choice geparst werden kann (Aufrufer behandelt das als NotEvaluated).
    /// </summary>
    Task<TypeSafeJudgmentAnswer?> AskConformanceAsync(string stateJson, CancellationToken cancellationToken);
}
