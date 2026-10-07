# G04 – revisionsgebundene BPMN-Dateiliste

Basis: `a9d16c8`; Branch `codex/git-source-control-phase-1`.
Status: Teilpaket implementiert und lokal geprüft. G04 bleibt insgesamt offen.

## Umsetzung

- `ControlledGitProcess.ListModelsAsync` liest den Baum eines nachgewiesenen festen
  Commitobjekts, ohne Checkout und ohne frei übergebene Gitargumente.
- `GitModelTree` prüft erlaubte Roots, relative Pfade, reguläre Dateien, Dateigrößen,
  Dateianzahl und Case-Kollisionen. Symlinks/Gitlinks innerhalb der erlaubten Roots
  werden abgewiesen. Strikte UTF-8-Dekodierung verhindert stille Pfadersetzung.
- Nur BPMN-Dateien; ordinale Sortierung, begrenzte Seitengröße. Der Cursor ist an
  Repository-ID, Commit und Root gebunden. Ein Cursor eines anderen Standes oder
  eine unbekannte Fortsetzungsposition ist ungültig.
- Die vollständige Modellanzahl aller konfigurierten Roots wird vor Paging geprüft;
  Aufteilung in kleine Seiten umgeht nicht `MaxModelFiles`.
- Gemeinsame Deadline und Cancellation für Git-Aufrufe sowie Baumprüfung.
  Metadata-Ausgabe bleibt begrenzt; übergroße Bäume werden nicht still abgeschnitten.
- Keine Registrierung eines Produktproviders, keine API/UI, keine Cloud-/GitHub-Writes.

## Prüfstand

Compiler-Diagnosebuild des lokalen Adapterprojekts: 0 Fehler, 8 Bestandswarnungen.
Analyzer explizit deaktiviert wegen der unabhängig dokumentierten strikten Buildlücken.
Formatprüfung `dotnet format whitespace ... --verify-no-changes --include .../GitModelTree.cs`:
Exit 0. Keine automatische Formatierung bestehender Dateien.

Neue tatsächliche Git-/TLS-Fälle in `GitHttpsTransportTests`:

- `Model_tree_pages_are_revision_bound_ordered_and_quota_limited`
- `Model_tree_rejects_link_entries_in_allowed_roots`

Erster gezielter Lauf: 2 gesamt, 1 bestanden, 1 fehlgeschlagen. Der Fehler war
`TimedOut` im bestehenden HTTPS-Fetch bei unverändertem Zehn-Sekunden-Limit,
vor Aufruf der Dateiliste. Unveränderte Wiederholung: **2 bestanden, 0 Fehler/Skips**,
16,049 Sekunden. Das bekannte Transport-Stabilitätsrisiko ist damit nicht abschließend
diagnostiziert oder behoben; keine Grenze erhöht, Assertion entfernt oder Retry eingebaut.

Finaler lokaler Adapterlauf: **88 bestanden, 0 Fehler, 0 Skips**, 87,474 Sekunden.
CI-sichere Regression mit unveränderten Workflow-Ausschlüssen: **1397 gesamt,
1389 bestanden, 0 Fehler, 8 bestehende externe Opt-in-Skips**, 98,024 Sekunden.
Vorheriger aktueller Compiler-Diagnosebuild des Haupttestprojekts: 0 Fehler,
330 Bestandswarnungen, Analyzer deaktiviert. Kein erfolgreicher strikter Build.

Lokaler Testbefehl nach aktuellem Build:

```powershell
$env:VERTEXBPMN_TEST_GIT = (Get-Command git).Source
dotnet test tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --no-build --no-restore --filter-not-class '*PostgresAcceptanceTests' --max-parallel-test-modules 1
```

Keine PostgreSQL-/SQL-Server-/Linux-/GitHub-/Browserabnahme. Keine Änderungen am
GitHub-Workflow oder dessen lokalen/externalen Ausschlüssen.

## Clean-Code-Prüfung

Scope: drei geänderte C#-Dateien; neue/geänderte Methoden und angrenzende Aufrufer,
nicht sämtliche unveränderten Altmethoden. D1–D7 ausgeführt, Änderungen manuell gelesen.

| Regel | Ergebnis im Änderungsscope |
| --- | --- |
| Async/Cancellation | awaitbare I/O, gemeinsame Deadline, Token in Parser propagiert |
| Collection-Verträge | materialisierte, begrenzte Page; keine null-Collection |
| Boolean-Modusschalter | keine neuen Schalter |
| Verantwortungsnamen | Baumdekodierung/-prüfung separat vom Prozessadapter |
| Regions/Struktur | keine neuen Regions |
| Interfaces/Grenzen | vorhandene Produktverträge unverändert |
| Vererbung | keine neue Vererbung |
| Lesbarkeit | Dekodierung extrahiert, Cursorlimit benannt, neuer Parser formatgeprüft |

Dies ist weder vollständiger Security-Audit noch Nachweis eines strikten Builds.

## Weiter offen

G03-Identitätsauflösung/automatischer Dispatch/Prozessneustart; G04-Branchliste,
History, Diff, vollständiger Produktprovider und Remote-CAS-Push/Reconciliation;
G05/API und G06/Studio. Low-Level-Adapter ersetzen keine Tenant-/Actor-/ACL-Prüfung
am zukünftigen autorisierten Application-Einstieg. Keine Paketcheckbox geschlossen.
