# G04 – begrenzter revisionsgebundener Snapshot-Diff

Basis: `9db4cb8`; Branch `codex/git-source-control-phase-1`.
Status: internes Teilpaket implementiert und lokal geprüft, Gesamtauftrag nicht fertig.

## Umsetzung

- `ControlledGitProcess.CompareModelAsync`: vergleicht ausschließlich einen sicheren
  BPMN-Snapshot mit den Originalbytes eines festen Commitobjekts. Bereits vorhandene
  Pfad-/Root-/Modus-/XML-/Secret- und Größenprüfungen bleiben aktiv.
- Identische Bytes liefern einen leeren Diff. Bei Änderungen schreibt `hash-object`
  nur immutable Blobs in den isolierten Workspace; weder Checkout noch Index/Ref
  noch Definitionsdatenbank werden geändert.
- Blob-Diff mit `--no-ext-diff`, `--no-textconv`, `--no-color`, `--no-renames`,
  `--text`. Keine Repository-Filter oder fremden Diffprogramme.
- Gemeinsame Read-Deadline/Cancellation und vorhandene Ausgabe-/Workspacequoten.
  Überschreitung liefert `PayloadTooLarge`, niemals einen unvollständigen Erfolgsdiff.
- Kein neues öffentliches DTO, keine Migration, kein CI-Ausschluss, kein Remote-Write.
  Grundlage: [offizielle Git-Diff-Dokumentation](https://git-scm.com/docs/git-diff).

## Tatsächliche Prüfungen

`GitHttpsTransportTests.Snapshot_diff_uses_pinned_bytes_without_filters_refs_or_runtime_changes`:
echter TLS-Fetch, unveränderter Snapshot, erwartete Änderung, Originalbytes/Ref erhalten,
keine Hooks/Filter/Checkout, ungültiger Root, übergroßer Patch und Cancellation.
Quota-Fall prüft zuerst erfolgreichen Read bei derselben kleinen Quota, damit nicht
ein vorheriger Metadata-Read statt der eigentlichen Patchausgabe das Limit auslöst.

Gezielter erster Lauf (vor Ergänzung Quota/Cancel): 1 bestanden, 0 Fehler/Skips.
Finale vollständige nicht-PostgreSQL-Auswahl: **89 bestanden, 0 Fehler, 0 Skips**,
76,413 Sekunden Runnerzeit / 86,780 Sekunden Gesamtdauer.

```powershell
dotnet build tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -p:RunAnalyzers=false -p:EnforceCodeStyleInBuild=false -p:TreatWarningsAsErrors=false -m:1 --disable-build-servers
$env:VERTEXBPMN_TEST_GIT = (Get-Command git).Source
dotnet test tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --no-build --no-restore --filter-not-class '*PostgresAcceptanceTests' --max-parallel-test-modules 1
```

Compiler-Diagnosebuild: initial 0 Fehler/9 Bestandswarnungen, finale inkrementelle
Builds 0 Fehler/0 Warnungen. Analyzer deaktiviert nur im Diagnosebefehl, Konfiguration
unverändert. Kein erfolgreicher strikter Analyzer-/Solution-Build behauptet.
Aktueller Haupttestprojekt-Diagnosebuild: 0 Fehler/330 Bestandswarnungen.
CI-safe Regression mit exakt den bestehenden Workflow-Ausschlüssen:
**1397 gesamt, 1389 bestanden, 0 Fehler, 8 bestehende externe Opt-in-Skips**,
84,034 Sekunden Runnerzeit / 85,807 Sekunden Gesamtdauer. Vollständiges lokales
Log: `C:/Users/yrodriguez/AppData/Local/Temp/vertex-git-diff-ci-safe.log`.
Keine neuen Ausschlüsse, keine entfernten Assertions. Befehl wie
`.github/workflows/ci.yml`, zusätzlich `--timeout 20m` als Laufzeitgrenze.

## Clean-Code-Review

Scope: neue Diff-Methode und neue Regression in zwei bestehenden C#-Dateien;
D1–D7 ausgeführt, gesamte neue Methode/Test samt angrenzendem Prozess-/Readpfad
manuell gelesen. Kein repositoryweiter Review.

| Regel | Ergebnis im Änderungsscope |
| --- | --- |
| Async/Cancellation | Tokens bis zu jedem Prozess; gemeinsame Deadline; kein async void |
| Collection-Verträge | keine neue Collection-Schnittstelle oder null-Collection |
| Boolean-Modusschalter | keine neuen Modusschalter; bestehendes Truncated ist Ergebnisstatus |
| Verantwortungsnamen | explizites CompareModel/StoreBlob statt generischem Helper |
| Regions/Struktur | keine neue Region; kurze lokale Bloboperation |
| Interfaces/Grenzen | Produktvertrag unverändert; interner Baustein ersetzt keine Autorisierung |
| Vererbung | keine neue Vererbung |
| Lesbarkeit | Guard für gleiche Bytes, feste Optionen, begründete Nebenwirkungen/Limitpolicy |

Keine bestätigten Clean-Code-Funde in den neuen Methoden; Format-Diffcheck bestanden.
Keine Formatprüfung des gesamten Bestands und kein vollständiger Security-Audit.

## Nicht abgeschlossen / Entscheidung erforderlich

- Branchliste und vollständige History: aktueller Fetch nutzt `--depth=1`; vollständige
  Historie braucht begrenztes Nachladen und darf shallow Ergebnisse nicht als vollständig
  melden. Kein History-Endpunkt vorgetäuscht.
- Diff-Paging, Anzeige für weitere XML-Encodings und API-/Studioanschluss noch offen.
- Remote-CAS-Push und dauerhafte Push-Reconciliation bleiben offen.
- G03 braucht verbindliche aktuelle Actor-/Tenant-/Aktivstatus-/Rollenquelle. Der bestehende
  `IIdentityService.UserInfo` enthält weder Rollen noch Aktivstatus;
  `PersistentIdentityService.ValidateUserAsync` verweist explizit auf den externen IdP.
  Lokale `User.Roles` beweisen keinen aktuellen OIDC-Rechteentzug. Vor automatischem
  Dispatch muss entschieden werden: Vertex-DB als eigenständige autoritative Git-Rechtequelle
  oder aktuelle Abfrage/Synchronisation des externen IdP. Keine gespeicherten Browserrollen
  als aktuelle Berechtigung verwenden, Worker bis dahin nicht automatisch aktivieren.
- G05/API und G06/Studio insgesamt offen. Keine G03/G04/G05/G06-Checkbox geschlossen.
- Keine neue PostgreSQL-/SQL-Server-/Linux-/Browser-/GitHub-Abnahme. Bekannter sporadischer
  TLS-Fetch-Timeout ist trotz bestandenem aktuellen Lauf nicht abschließend behoben.
- Commit/Push dieses Teilpakets vom Nutzer anschließend ausdrücklich freigegeben;
  dadurch werden keine offenen Produkt- oder Abnahmekriterien geschlossen.

Nächster Schritt nach Identitätsentscheidung: Resolver/Dispatch und Prozessneustartprüfung,
parallel unabhängige Branch-/Historyadapter; dann Remote-Push, Provider, autorisierte API,
Studio und reale lokale End-to-End-Abnahme in der Reihenfolge des Hauptplans.
