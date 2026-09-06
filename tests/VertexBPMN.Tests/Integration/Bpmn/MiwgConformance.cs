using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VertexBPMN.Tests.Integration.Bpmn
{
    /// <summary>
    /// Conformance-Baseline der BPMN-2.0-MIWG-Referenzmodelle (OMG 2025, 21 Dateien).
    ///
    /// Dient als strenges Gate ueber die vollstaendige MIWG-Suite:
    ///  - Besitzt ein Modell den Status Completed=true, DARF es nicht fehlschlagen (Regression -> Test rot).
    ///  - Besitzt ein Modell den Status Completed=false, DARF es nicht auf einmal durchlaufen
    ///    (unerwartete Verbesserung -> Test rot, damit die Baseline gepflegt und das Feature dokumentiert wird).
    ///
    /// So ist die Suite gruen, obwohl 8/21 Modelle bekannte, dokumentierte Luecken haben -
    /// und jede Aenderung in die eine oder andere Richtung wird sichtbar erzwungen.
    ///
    /// Stand: 2026-09-06, gemessen gegen Engine (Release, net10.0).
    /// Durchlaufend (Completed=true): 17/21
    /// Dokumentiert offen (Completed=false): 4/21
    /// </summary>
    public static class MiwgBaseline
    {
        public sealed record Entry(string File, bool Completed, string Note);

        public static readonly IReadOnlyDictionary<string, Entry> Files =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase)
            {
                ["A.1.0.bpmn"] = new("A.1.0.bpmn", true,  "Basisprozess (Start -> Ende, parallele Gateways)."),
                ["A.2.0.bpmn"] = new("A.2.0.bpmn", true,  "Exklusives Gateway mit FEEL-Conditions."),
                ["A.2.1.bpmn"] = new("A.2.1.bpmn", true,  "Exklusives Gateway - Variante."),
                ["A.3.0.bpmn"] = new("A.3.0.bpmn", true,  "Datenobjekte/DataInput im Prozess."),
                ["A.4.0.bpmn"] = new("A.4.0.bpmn", true,  "Basisprozess mit Sequenzflussvarianten."),
                ["A.4.1.bpmn"] = new("A.4.1.bpmn", true,  "Basisprozess - Variante."),
                ["B.1.0.bpmn"] = new("B.1.0.bpmn", true,  "Multi-Instance/Branchensteuerung."),
                ["B.2.0.bpmn"] = new("B.2.0.bpmn", true,  "Fehlerbehandlung/Endereignisse."),
                ["C.1.0.bpmn"] = new("C.1.0.bpmn", true,  "Start ueber getyptes Start-Event; Auto-Start feuert getypte Starts, wenn kein none-Start existiert."),
                ["C.1.1.bpmn"] = new("C.1.1.bpmn", false, "PENDING (interaktiv): User-Task-Outputs 'approved'/'clarified' fehlen in eingabeloser Ausfuehrung. Mit Inputs vollstaendig ausfuehrbar - siehe MIWGInteractiveInputSuite."),
                ["C.2.0.bpmn"] = new("C.2.0.bpmn", true,  "Getyptes Start-Event; Auto-Start feuert getypte Starts (kein none-Start)."),
                ["C.3.0.bpmn"] = new("C.3.0.bpmn", true,  "Getyptes Start-Event; Auto-Start feuert getypte Starts (kein none-Start)."),
                ["C.4.0.bpmn"] = new("C.4.0.bpmn", true,  "Erweiterte Flusssteuerung/Subprozess."),
                ["C.5.0.bpmn"] = new("C.5.0.bpmn", true,  "Weitere Prozessvariante."),
                ["C.6.0.bpmn"] = new("C.6.0.bpmn", true,  "Getyptes Start-Event; Auto-Start feuert getypte Starts (kein none-Start)."),
                ["C.7.0.bpmn"] = new("C.7.0.bpmn", true,  "'Advertise a job vacancy' (Kollaboration mit Ressourcen) - laeuft nach Parser-Hack-Fix durch."),
                ["C.8.0.bpmn"] = new("C.8.0.bpmn", false, "PENDING (interaktiv): DMN-Variable 'Vacation Approval' erst durch Benutzer-/DMN-Eingabe. Mit Input vollstaendig ausfuehrbar - siehe MIWGInteractiveInputSuite."),
                ["C.8.1.bpmn"] = new("C.8.1.bpmn", false, "PENDING (interaktiv): DMN-Variable 'Vacation Approval' - siehe C.8.0, MIWGInteractiveInputSuite."),
                ["C.9.0.bpmn"] = new("C.9.0.bpmn", false, "PENDING (interaktiv): DMN-Decision-Output 'riskLevels' fehlt in eingabeloser Ausfuehrung (FEEL-Quantor funktioniert bei vorhandenem Input). Mit Input ausfuehrbar - siehe MIWGInteractiveInputSuite."),
                ["C.9.1.bpmn"] = new("C.9.1.bpmn", true,  "Risiko/Variablenprozess - Variante."),
                ["C.9.2.bpmn"] = new("C.9.2.bpmn", true,  "Risiko/Variablenprozess - Variante."),
            };

        /// <summary>Alle MIWG-Referenzdateien (*.bpmn) im TestData/Reference-Ordner, sortiert.</summary>
        public static IEnumerable<string> ReferenceFiles()
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference");
            if (!Directory.Exists(dir))
            {
                Console.WriteLine($"MIWG BPMN test dir not found: {dir}");
                yield break;
            }
            foreach (var file in Directory.GetFiles(dir, "*.bpmn").OrderBy(f => f))
                yield return file;
        }

        public static void EnsureCompleteCoverage(IEnumerable<string> files)
        {
            var listed = Files.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
            {
                var name = Path.GetFileName(f);
                if (!listed.Contains(name))
                    throw new InvalidOperationException(
                        $"MIWG-Baseline unvollstaendig: '{name}' fehlt im Baseline-Woerterbuch. Bitte Eintrag ergaenzen.");
            }
        }
    }
}
