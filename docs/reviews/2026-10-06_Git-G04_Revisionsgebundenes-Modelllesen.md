# G04 – revisionsgebundenes BPMN-Modelllesen

Basis: `02e7a42`; Branch `codex/git-source-control-phase-1`.
Status: implementiertes Teilpaket, G04 insgesamt offen. Keine Paketcheckbox geschlossen.

## Umsetzung

- `ControlledGitProcess.ReadModelAsync`: liest ausschließlich aus einem nachgewiesenen
  Commitobjekt. Keine Branchauflösung, kein Checkout, keine XML-Neuserialisierung.
- Vollständige, begrenzte Baumprüfung: exakter Modellpfad, regulärer Blob,
  keine abweichende Groß-/Kleinschreibung, Symlink-/Gitlink-Ziele oder unsichere Vorfahren.
- Objektgröße vor Inhaltslesen geprüft; Inhalt zusätzlich beim Lesen begrenzt.
  Modellbytes nutzen `MaxModelBytes`, nicht das kleinere `MaxDiffBytes`.
- Gesamter Lesepfad hat eine gemeinsame Deadline und propagiert Cancellation.
  Die vorhandene BPMN-/Secret-/DTD-Policy prüft die Originalbytes vor Rückgabe.
- `GIT_NO_REPLACE_OBJECTS=1` verhindert die Substitution gepinnter Object-IDs.
  Rohes Git-stderr und Modellinhalte werden nicht protokolliert.
- `GitHttpsTransportTests`: echte TLS-/Git-Fixtures für Originalbytes, feste alte/neue
  Revisionen, Großmodell über 1 MiB, Replace-Ref-Manipulation, Größenlimit,
  fehlende Datei, Traversal, Rootverletzung, Case-Mismatch, Symlink und Gitlink.

Git-Primitiven gegen Primärdokumentation geprüft:
[cat-file](https://git-scm.com/docs/git-cat-file), [ls-tree](https://git-scm.com/docs/git-ls-tree).

## Prüfstand

Compiler-Diagnosebuild des lokalen Adapterprojekts erfolgreich, 0 Fehler,
8 bestehende Warnungen beim letzten nicht inkrementellen Lauf. Analyzer waren
explizit deaktiviert; dies ist kein erfolgreicher strikter Gesamtbuild.

Vor dem letzten Replace-Ref-Schutz: zwei neue gezielte Tests und anschließend
86 lokale Adaptertests bestanden, 0 Fehler/Skips. Nach kleiner Methodenextraktion
erneut 86 bestanden, 0 Fehler/Skips. Finaler lokaler Lauf einschließlich Replace-Ref-
Schutz: **86 bestanden, 0 Fehler, 0 Skips**, 53,803 Sekunden.
CI-sichere Regression mit unveränderten Workflow-Ausschlüssen: **1397 gesamt,
1389 bestanden, 0 Fehler, 8 bestehende externe Opt-in-Skips**, 89,954 Sekunden.
Frischer Haupttest-Compiler-Diagnosebuild davor erfolgreich. Auch hier waren
Analyzer deaktiviert; keine strikte Buildfreigabe daraus ableiten.

Lokaler Befehl nach aktuellem Build:

```powershell
$env:VERTEXBPMN_TEST_GIT = (Get-Command git).Source
dotnet test tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --no-build --no-restore --filter-not-class '*PostgresAcceptanceTests' --max-parallel-test-modules 1
```

Keine PostgreSQL-/SQL-Server-/Linux-/GitHub- oder Browserabnahme durchgeführt.
Lokale Native-Git-Tests bleiben im getrennten Projekt; GitHub-Workflow unverändert.

## Grenzen und nächste Umsetzung

Dieser interne Baustein ersetzt weder Repository-ACLs noch Tenant-/Actorprüfung
im zukünftigen Application-/Provider-Einstieg. Noch keine benutzbare Git-UI.
Offen bleiben G03-Identitätsauflösung/Queue-Dispatch/Prozessneustart, vollständige
G04-Reads (Branches, Dateiliste, History, Diff), Remote-CAS-Push und Reconciliation.
Der bekannte strikte Analyzer-Gesamtbuild bleibt unabhängig davon offen.

Clean-Code-Prüfung: D1–D7 auf den zwei geänderten C#-Dateien ausgeführt;
geänderte Methoden und angrenzende Aufrufer manuell geprüft. Baumprüfung als
private reine Funktion ausgelagert; bestehende öffentliche Verträge unverändert.
Keine behauptete vollständige Prüfung aller unveränderten Methoden oder Security-Abnahme.
