# Git-Integration im Studio ausblenden

## Verhalten

Der bestehende API-Schalter `SourceControl:Enabled=false` deaktiviert die Integration
und blendet jetzt auch den Git-Arbeitsbereich im BPMN-Editor aus. Alternativ kann
`SourceControl__Enabled=false` als Umgebungsvariable gesetzt werden. API neu starten
und die Studio-Seite neu laden; es gibt keinen zusätzlichen Studio-Schalter.

Das Studio fragt `/api/source-control/availability` erst nach Beginn des interaktiven
Blazor-Circuits ab. Bis zum Ergebnis werden keine Git-Bedienelemente gerendert.
Nur der explizite Zustand `Disabled` blendet den Bereich aus. API-Fehler und fehlende
Gitinstallation lassen Diagnose und erneute Verbindungsprüfung erreichbar.
Die erste Prüfung ist auf fünf Sekunden begrenzt, auch wenn die allgemeine
HTTP-Resilience-Pipeline bei einem Ausfall wiederholte Versuche vornehmen würde.

Repositorybindungen, Credentials, gespeicherte Aufträge und Editorinhalte werden
nicht gelöscht oder verändert. API-Autorisierung und Backend-Deaktivierung bleiben
unverändert. Das Ausblenden ersetzt keine serverseitige Zugriffskontrolle.

## Regression

`tests/VertexBPMN.Studio.UiTests/StudioGitVisibilityTests.cs` enthält lokale
Playwright-Fälle für aktivierte Integration, deaktivierte Integration, fehlendes Git
und HTTP-503 bei der Verfügbarkeitsprüfung. Alle Fälle prüfen zusätzlich den
sichtbaren BPMN-Canvas und das Ausbleiben schreibender Git-API-Aufrufe. Der Host
verwendet deterministische API-Antworten; dies ist keine reale GitHub-Abnahme.
Der Test bleibt im lokalen UI-Testprojekt und wird nicht dem CI-Workflow hinzugefügt.

Ausgeführt mit dem direkten xUnit-Runner:
`dotnet tests/VertexBPMN.Studio.UiTests/bin/Release/net10.0/VertexBPMN.Studio.UiTests.dll -class '*StudioGitVisibilityTests'`.
Abschließendes Ergebnis: **4 bestanden**, 0 Fehler, 0 übersprungen (28,723 Sekunden).
Der erste HTTP-503-Lauf scheiterte an der zu langen initialen Verfügbarkeitsprüfung;
die oben beschriebene Frist behebt diesen konkreten Fehler. Kein sichtbarer
Computer-Use-Lauf und keine vollständige CI-safe-/Gesamtregression durchgeführt.

Clean-Code-Nachprüfung der geänderten C#-Mitglieder mit D1–D7 und manueller
Kontrollflussprüfung: keine bestätigten neuen Befunde. Cancellation der ersten
HTTP-Abfrage folgt Komponentenlebensdauer und Frist; Test-Host-Prozesse werden im
`finally` beendet. Bestehende Komponenten-/Host-Implementierung nicht global refaktoriert.

Diagnosebuild erfolgreich. Der strenge Build ist nicht erfolgreich: zunächst
MA0048 für den zuvor eingeführten Record `PullRequestReview` (jetzt ohne
Vertragsänderung in eigene Datei verschoben), anschließend 354 bestehende
Studio-Analyzer-/Warnungsfehler. Die Analyzer-Konfiguration wurde nicht verändert.
