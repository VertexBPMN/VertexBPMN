# G04 – Remote-Branchliste und vollständige begrenzte BPMN-Historie

Basis: `c8073f8`; Branch `codex/git-source-control-phase-1`.
Status: interne Bausteine implementiert und lokal geprüft. Kein vollständiger
G04-Provider, keine API-/Studioabnahme und keine Paketcheckbox geschlossen.

## Umsetzung und geänderte Grenzen

- `ControlledGitProcess.ListRemoteBranchesAsync`: fragt echte Remote-Heads mit
  `ls-remote --branches --refs` ab, keine scheinbare Branchliste aus `vertex-source`.
  Gleiche HTTPS-Allowlist, Public-DNS-Prüfung und IP-Pinning wie Fetch; Read-Deadline,
  vorhandene Ausgabe-/Workspacequoten und serverseitiger Credentialkanal.
- `RunAuthenticatedRemoteAsync` kapselt die bisherige feste Helper-/TLS-Konfiguration
  gemeinsam für Fetch und Branchabfrage. Privat, kein beliebiger Kommandoendpunkt.
  Hooks, Filter, Prompts, Redirects und ambient Credentials bleiben gesperrt.
  Local-Acceptance-Einstiege erlauben ausschließlich HTTPS/localhost auf Sonderport
  mit explizitem CA-Zertifikat; kein konfigurierbarer Produktions-Bypass.
- `GitRemoteReferences`: strikte UTF-8-Dekodierung, vollständige Object-IDs,
  ausschließlich heads, Pfad-/Refvalidierung, keine doppelte/case-kollidierende
  Branchliste, ordinale Sortierung und begrenzte Pages. Fingerprint umfasst
  Repository/Tenant/Remote sowie alle Namen UND Head-OIDs; Reset oder neue Heads
  zwischen Seiten liefern `RevisionConflict`, kein stiller Kontextwechsel.
- `FetchHistoryAsync`: eigener Fetch der festgehaltenen Commit-ID mit begrenzter
  Tiefe. Der bestehende normale Read-/Commit-Fetch bleibt bei `--depth=1`.
  Auch wenn der Defaultbranch inzwischen bewegt wurde, wird der feste Commit geladen.
- Neues optionales `SourceControl:Limits:MaxHistoryCommits`: Default **1000**, zulässig
  **1–10000**. Kein Git-/Secretzwang im deaktivierten Profil. Keine neue Abhängigkeit,
  keine Migration und keine Änderung am schnellen GitHub-Workflow.
- `ReadModelHistoryAsync`: vorhandene feste Commit-/Modus-/BPMN-/Pfadprüfung,
  gemeinsames Read-Zeitbudget; shallow Repositories werden als unvollständig abgewiesen.
  Vollständigen erreichbaren Commitgraph vor Dateifilterung begrenzen; auch ein selten
  geändertes Modell darf das Graphlimit nicht durch eine kleine Page umgehen.
- Literale Pathspecs, keine Notes/Signaturen/Decorations, feste topologische Reihenfolge.
  `GitModelHistory` verarbeitet NUL-getrennte ID/Zeit/Subject-Metadaten, prüft UTF-8,
  Zeitbereich, Controlzeichen, Duplikate und Subjectlänge. Cursor an Repository,
  Tenant, Remote, Commit und Modellpfad gebunden; Fortsetzung muss vorhanden sein.

Technische Grundlagen am 2026-10-06 geprüft:
[Git ls-remote](https://git-scm.com/docs/git-ls-remote),
[Git fetch](https://git-scm.com/docs/git-fetch),
[Git log](https://git-scm.com/docs/git-log). Diese Dokumentation ersetzt keine Tests.

## Tatsächliche lokale Nachweise

Neue native TLS-/Git-Fälle in `GitHttpsTransportTests`:

- `Remote_branch_pages_use_authenticated_heads_and_reject_stale_or_foreign_cursors`:
  Remote-Heads statt Clientcache, echte Authentication-Challenge, Paging, fremde
  Repositorybindung, neue Remote-Ref, ungültige Page vor Netzwerkzugriff, Tags nicht
  als Branches, kein Checkout/Hook und kein Fixturetoken in lokalen Artefakten.
- `History_fetch_is_revision_pinned_complete_paged_and_rejects_shallow_or_over_limit_graphs`:
  echte mehrere Modellcommits, ursprünglicher shallow Read abgewiesen, Defaultbranch
  nach Auswahl bewegt, feste Revision mit kompletter Historie nachgeladen, drei
  Pages bis Ursprung, falscher Commitcursor und Graphquota bei PageSize=1 abgewiesen.

`GitRemoteReferencesTests`: vier malformed Antwortvarianten; zusätzlich reiner
Headreset bei unveränderten Namen, Cursorlänge, ungültiges UTF-8 und leere Liste.
`GitModelHistoryTests`: fünf malformed Varianten einschließlich Zeitüberlauf,
NUL-/Controlfehler; zusätzlich Repo-/Pfadbindung, Graphquota, UTF-8 und leere Liste.
`SourceControlOptionsTests`: drei unzulässige Historylimits (0, -1, 10001).

Erster gezielter Branchlauf: **1 bestanden, 0 Fehler/Skips**, 10,647 Sekunden Runnerzeit.
Finale vollständige lokale nicht-PostgreSQL-Auswahl: **102 bestanden, 0 Fehler,
0 Skips**, 119,598 Sekunden Runnerzeit / 119,884 Sekunden Gesamtdauer.

```powershell
dotnet build tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -p:RunAnalyzers=false -p:EnforceCodeStyleInBuild=false -p:TreatWarningsAsErrors=false -m:1 --disable-build-servers
$env:VERTEXBPMN_TEST_GIT = (Get-Command git).Source
dotnet test tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --no-build --no-restore --filter-not-class '*PostgresAcceptanceTests' --max-parallel-test-modules 1
```

Native Git: weiterhin Git for Windows 2.56.0. Lokaler Compiler-Diagnosebuild:
0 Fehler/9 Bestandswarnungen. Frischer Haupttestprojekt-Diagnosebuild: 0 Fehler/
330 Bestandswarnungen. Analyzer nur im Diagnosebefehl deaktiviert; repositoryweite
Analyzerkonfiguration unverändert. Kein erfolgreicher strikter Solution-Build.
Formatprüfung der vier neuen C#-Dateien (`dotnet format whitespace ...
--no-restore --verify-no-changes --include ...`): Exit 0. Git-Diffcheck: Exit 0.

CI-safe Gesamtsuite mit exakt den bestehenden Workflow-Ausschlüssen:
**1400 gesamt, 1392 bestanden, 0 Fehler, 8 bestehende externe Opt-in-Skips**,
95,432 Sekunden Runnerzeit / 97,692 Sekunden Gesamtdauer. Keine neuen Ausschlüsse
oder geschwächten Assertions. Befehl wie `.github/workflows/ci.yml`, zusätzlich
`--timeout 20m` als Laufzeitgrenze.

Lokale Logs in `C:/Users/yrodriguez/AppData/Local/Temp/`:
`vertex-git-branches-build.log`, `vertex-git-history-build.log`,
`vertex-git-branch-history-local.log`, `vertex-git-branch-history-main-build.log`,
`vertex-git-branch-history-ci-safe.log`, `vertex-git-branch-history-format.log`.

Diagnostische Fehlaufrufe transparent festhalten: Der Filter `*Git*Tests` fand
0 Tests (Exit 5), weil Wildcards nur am Anfang/Ende unterstützt sind; kein bestandenes
Ergebnis. `dotnet test ... -- --help` löste einen SDK-Hilfe/IPC-FailFast aus, ebenfalls
kein Produkt-/Testnachweis. Direkte Runnerhilfe gelesen, danach die vorhandene ganze
nicht-PostgreSQL-Auswahl erfolgreich ausgeführt. Keine Testfälle entfernt.

## Clean-Code-Review

Scope: neun C#-Dateien, eigene neue/geänderte Methoden plus betroffene Aufrufer;
keine erzeugten Dateien, kein Bestands-/Gesamtrepositoryreview. D1–D7 ausgeführt;
beide neue Parser und neuer Transport-/Historypfad samt Tests manuell gelesen.

| Regel | Ergebnis im Änderungsscope |
| --- | --- |
| Async/Cancellation | awaitbare I/O und gemeinsame Deadlines; reine Parser propagieren Token |
| Collections | materialisierte begrenzte Pages, keine null-Collections |
| Boolean modes | keine neuen Modusschalter; Fetch-Tiefe ist expliziter Mengenwert |
| Verantwortungsnamen | Remote-Refs und History-Metadaten separat vom Prozessadapter |
| Regions | keine neuen Regions |
| Interfaces | öffentliche Providerports unverändert, kein neues unnötiges Interface |
| Vererbung | keine neue Vererbung |
| Lesbarkeit | Guardclauses, benannte Cursor-/Historygrenzen, neue Dateien formatgeprüft |

Keine bestätigten Clean-Code-Funde in den eigenen Änderungen. Die Prüfung beweist
weder Produktvollständigkeit noch vollständige Security- oder Analyzerabnahme.

## Weiter offen

- In diesem Paket keine PostgreSQL-/SQL-Server-/Linux-/Browser-/GitHub-Abnahme.
  Vorhandene alte Nachweise bleiben historisch; SQL Server weiterhin vom Nutzer ausgenommen.
- Graphlimit gilt für alle Vorfahren, nicht nur Modelländerungen. Shallow in einem
  anderen Ref des gleichen privaten Repositorys kann konservativ den Read blockieren;
  kompletter Graph wird ausschließlich nach erfolgreichem Nachladen ausgewiesen.
- Branch-Paging holt alle Heads innerhalb der Ausgabequote erneut; bei Headänderung
  muss der Anwender neu beginnen. Keine verteilte Snapshotcache-/Performanceabnahme.
- Automatischer Worker/aktuelle Identitätsquelle, realer Worker-Prozessneustart,
  Remote-CAS-Push/Reconciliation, vollständiger Produktprovider, G05/API und G06/Studio.
- Bekannter sporadischer Fetch-Timeout ist durch dieses Paket nicht als behoben erklärt.
- Keine automatische Commit/Push-/PR-Aktion im Auftrag „mach weiter“.

Nächster unabhängiger Schritt: sicherer atomarer Remote-Push samt dauerhafter
Result-Reconciliation. Worker bleibt bis zur verbindlichen Identitätsquelle fail-closed.
