namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>Parsiertes Ergebnis einer TypeSafe-Choice-Evaluierung.</summary>
public sealed record TypeSafeJudgmentAnswer(
    string Choice,
    double Confidence,
    IReadOnlyDictionary<string, double> Probabilities);
