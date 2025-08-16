using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Core.Handlers
{
    public class CalculateScoreServiceTaskHandler : IServiceTaskHandler
    {
        public async Task ExecuteAsync(
            IDictionary<string, string> attributes,
            IDictionary<string, object> variables,
            CancellationToken ct = default)
        {
            // Extrahiere notwendige Variablen
            if (!variables.TryGetValue("applicantName", out var applicantName) || string.IsNullOrWhiteSpace(applicantName?.ToString()))
            {
                throw new InvalidOperationException("Missing or invalid 'applicantName' variable.");
            }

            if (!variables.TryGetValue("age", out var age) || !int.TryParse(age?.ToString(), out var applicantAge))
            {
                throw new InvalidOperationException("Missing or invalid 'age' variable.");
            }

            // Simuliere die Berechnung des Scores
            var creditScore = CalculateCreditScore(applicantName.ToString(), applicantAge);

            // Speichere das Ergebnis in den Prozessvariablen
            variables["creditScore"] = creditScore;

            // Logge das Ergebnis (optional)
            await Task.CompletedTask; // Simuliert asynchrone Verarbeitung
        }

        private int CalculateCreditScore(string applicantName, int age)
        {
            // Beispiel-Logik: Höheres Alter ergibt einen höheren Score
            var baseScore = 500;
            var ageFactor = age * 2;

            // Zusätzliche Logik kann hier hinzugefügt werden
            return baseScore + ageFactor;
        }
    }
}