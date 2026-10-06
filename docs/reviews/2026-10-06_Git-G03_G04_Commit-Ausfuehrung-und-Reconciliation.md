# G03/G04 – lokale Commit-Ausführung und Wiederanlauf

Basis: `0fb81d66fe9e32d40fd069f36943a6a364a43c83`, Branch `codex/git-source-control-phase-1`. Stand: 2026-10-06.

## Umsetzung

- `PersistentSourceControlStore.EnqueueCommitAsync` speichert Annahmezeit und Bindingrevision in einem geschützten Request der Schemaversion 2. Der Idempotenzhash bleibt an den bisherigen kanonischen Nutzereingaben und der Bindingrevision gebunden; die serverseitige Zeit ist kein Grund, einen identischen Retry abzulehnen.
- `ReadCommitWorkAsync` prüft aktuellen Actor/Tenant, Leaseinhaber, Fence, Zustand, Commitart, frische ACL/Rollen, Bindingrevision und Request-/Snapshot-Hashes. Es liest die angenommenen Bytes, nicht eine inzwischen weiterbearbeitete Sitzung. Schema-1-Aufträge ohne belegte Annahmezeit werden fail-closed abgelehnt: keine nachträglich erfundene Zeit und keine automatische Wiederholung alter unklarer Writes.
- `SourceControlCommitExecutor` ist ein scoped, endlicher interner Executor für bereits geclaimte Aufträge. Er erstellt deterministische echte Commitobjekte und speichert vor dem Ref-Write einen geschützten Beleg mit Commit-ID, Snapshot-Hashes, Session und zugeordnetem Workspace-Fence.
- `PublishLocalCommitAsync` hält Binding- und Operations-Schreibsperren in einer DB-Transaktion während der lokalen Ref-Publikation. Rechte-/Bindingänderung und ein neuer Lease-Inhaber können nicht gleichzeitig diesen Write freigeben. Ein Datenbank-Rollback nach dem Git-Effekt lässt den zuvor gespeicherten Beleg erhalten.
- Git publiziert nur den expliziten privaten Arbeitsbranch. Ein fehlender Ref wird mit erwarteter Null-OID erzeugt; identischer vorhandener Commit wird abgeglichen, ein anderer Commit nicht überschrieben. Symbolische Verweise werden abgelehnt; `--no-deref` verhindert zusätzlich ein umgeleitetes Update. Grundlage: [Git update-ref](https://git-scm.com/docs/git-update-ref).
- Wiederanlauf öffnet ausschließlich den signierten, tenant-/actor-/operationsgebundenen Workspace aus dem Beleg. Der neue Fence wird geprüft; der frühere Worker kann nicht publizieren. Deterministischer Objektaufbau verifiziert Parent, Zeit, Nachricht und Bytes gegen die erwartete Commit-ID. Er wiederholt keinen bereits bestätigten Ref-Write.
- Ein unbekannter Auftrag ohne Beleg bleibt unbekannt statt blind neu ausgeführt zu werden. Fehlender Originalworkspace wird nicht durch einen beliebigen neuen Clone ersetzt.
- Initialer Fetch verwendet die angenommene vollständige Commit-ID, keinen veränderbaren Default-Branch als Ersatz. Wenn der Server diesen Commit nicht liefern kann, schlägt der Auftrag fehl. Grundlage: [Git fetch](https://git-scm.com/docs/git-fetch).
- Annahme und Ausführung erlauben nun konsistent mehrzeilige Commitnachrichten; sonstige Kontrollzeichen bleiben verboten.

Keine neue DB-Migration: die zusätzlichen unveränderlichen Metadaten liegen im bestehenden geschützten Request. Keine Änderung an Engine, Modeler oder generierten Studioassets. Bestehende SQLite-/WSLC-/deaktivierte Profile und CI-Filter bleiben erhalten.

## Lokale Nachweise

Separate native Adapter-Suite, ohne PostgreSQL-Klasse: **73 bestanden, 0 fehlgeschlagen, 0 übersprungen**, 45,275 s. Darin neun echte Git-/SQLite-Executorfälle und sieben HTTPS-Git-Fälle sowie bestehende Verträge/Negativfälle.

```powershell
$env:VERTEXBPMN_TEST_GIT = (Get-Command git).Source
dotnet test tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --filter-not-class '*PostgresAcceptanceTests' --max-parallel-test-modules 1
```

`CommitExecutionAcceptanceTests` prüft:

1. Exakte angenommene CRLF-Bytes und mehrzeilige Nachricht nach N+1-Edit, unveränderte Annahmezeit nach Leaserenewal, Parent/Branch/History und dauerhafter Status.
2. Echter Git-Ref-Write, dann per DB-Trigger absichtlich scheiternder Abschluss. Neuer DbContext/Executor und Fence gleichen denselben Commit ab; kein zweiter Commit.
3. Dauerhafter Beleg vor Ref-Write: neuer Fence publiziert den erwarteten Commit.
4. Unbekannter Auftrag ohne Beleg wird nicht wiederholt.
5. Abweichender lokaler Branch bleibt unverändert; Beleg bleibt erhalten.
6. Fremdtenant, ReadOnly und entzogene ACL blockieren die Ausführung.
7. Symbolischer Arbeitsbranch kann keinen Default-Branch-Write auslösen.
8. Zwei konkurrierende echte Git-Ref-Updates: genau ein Gewinner, ein Konflikt, Default-Branch unverändert.
9. Falsche gespeicherte Commit-ID wird vor Publikation abgelehnt.

HTTPS-Test `Accepted_revision_fetch_does_not_substitute_a_moved_default_branch`: Default-Branch wird tatsächlich zurückgesetzt, angenommener Commit bleibt über einen anderen Branch erreichbar. Echter HTTPS-Fetch lädt dennoch die bestätigte OID und Originalbytes; kein Fetch des neuen Default-Heads als Ersatz.

SQLite-Storeklasse im zentralen Testprojekt: **16 bestanden, 0 Fehler/Skips**, 20,935 s. Bestehende Snapshot-/Idempotenzassertions bleiben bestehen, ergänzt um Schemaversion, Annahmezeit und Bindingrevision.

### PostgreSQL unter WSLC

**3 bestanden, 0 Fehler, 0 übersprungen**, 15,676 s. `PostgresAcceptanceTests` enthält jetzt zusätzlich `Real_postgres_reconciles_git_effect_after_failed_database_finish` mit realem Git und PostgreSQL-Triggerfehler nach Ref-Publikation. Bestehende Upgrade-/Idempotenz-/Fencingprüfung und tatsächlicher WSLC-Containerneustart erneut bestanden.

Infrastruktur: isoliertes `postgres:17-alpine`, nur `127.0.0.1:55439`, bereits vorhandenes Image; keine Installation/Downloads oder Veränderungen bestehender Container. Jeder Lauf erzeugt und entfernt seine eigene zufällig benannte Testdatenbank. Nur im isolierten Container temporäres Trust-Login, keine Produktcredentials.

```powershell
# VERTEXBPMN_TEST_POSTGRES_ADMIN lokal für diese isolierte Instanz konfigurieren.
# VERTEXBPMN_TEST_WSLC und VERTEXBPMN_TEST_POSTGRES_CONTAINER für den Neustartfall setzen.
$env:VERTEXBPMN_TEST_GIT = (Get-Command git).Source
dotnet test tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --no-build --no-restore --filter-class '*PostgresAcceptanceTests' --max-parallel-test-modules 1
```

Cleanup bestätigt: ausschließlich `vertex-source-control-phase2-20261006-commit-recovery` und dessen anonymes Volume `474301323c751fe144d582343c85db5a25b47b1d057f4683c85a243be80a4ba5` gestoppt/entfernt. Vorhandene ÜBV-Container/Volumes unberührt. SQL Server bleibt auf Nutzerwunsch ungetestet.

Solution-Release-Build: **0 Fehler, 3 bestehende Warnungen im inkrementellen Lauf**, 18,33 s. Frühere Teilbuilds meldeten weitere vorhandene Azure-/Nullable-/Analyzerwarnungen; kein warning-freier Clean-Build behauptet.

### CI-safe Gesamtsuite: bestanden

Der unveränderte Workflow-Befehl mit `--no-build --no-restore` wurde nach dem Build gestartet und endete erfolgreich mit Exitcode 0: **1.388 gesamt, 1.380 bestanden, 0 fehlgeschlagen, 8 übersprungen**, Dauer **15 Minuten 27,138 Sekunden**. Die acht bestehenden externen Opt-in-Fälle wurden übersprungen.

Nach über elf Minuten lag zunächst noch kein Endergebnis vor. Drei Stack-Aufnahmen zeigten den bestehenden Fall `HistoricalPredictiveAnalyticsServiceTests.PredictDuration_UsesCompletedHistoricalInstancesForCurrentTenant` innerhalb `HistoricalPredictiveAnalyticsService.TrainDurationModel`, `pipeline.Fit(data)` und der ML.NET-SDCA-/Background-Threadpool-Ausführung; kein Git-Executorframe in diesem aktiven Teststack. Das dokumentiert ungewöhnliche Laufzeit, keine bestätigte dauerhafte Blockade oder abschließend geklärte Ursache. Der ML-Pfad, seine Assertions und die CI-Ausschlüsse wurden nicht verändert. Ein Abbruch-/Cleanupauftrag wurde blockiert und nicht ausgeführt; die Suite beendete sich anschließend selbst erfolgreich. Der Testprozess ist nicht mehr aktiv.

Für die reine Stackdiagnose wurde `dotnet-stack` 10.0.745401 ausschließlich in `C:\Users\yrodriguez\AppData\Local\Temp\vertex-git-diagnostics-097e6aaa9bb144bdb6dc0d11623af22f` installiert, nicht global. Auch dieser Ordner konnte wegen des blockierten Cleanupauftrags noch nicht entfernt werden. Kein kompletter Speicherdump erzeugt, keine Credentials/Prozessumgebungen ausgelesen. WSLC-Container/Volume-Cleanup davon unabhängig erfolgreich bestätigt.

## Offene Grenzen / nächster Schritt

### Nachtrag: Rollenentzug während der Ausführung

Auf Basis `5121665` verlangt `SourceControlCommitExecutor.ExecuteAsync` einen vom vertrauenswürdigen Host gelieferten Resolver statt statischer Rollen. Nach der Commitobjekt-Erzeugung und vor der Ref-Publikation werden Rollen erneut aufgelöst; `ReadCommitWorkAsync` prüft damit die aktuellen ACLs und die Lease. Der Host muss weiterhin denselben Actor/Tenant und einen aktiven Benutzer nachweisen. Dieser Resolver ist noch nicht mit einem gehosteten Worker/Identity-Backend verbunden. Die statische Rollenüberladung bleibt ausschließlich für isolierte Prepared-Workspace-Abnahmen.

Regression: `CommitExecutionAcceptanceTests.Role_revocation_during_execution_blocks_publication_and_preserves_recovery_intent` verwendet echte Gitobjekte und SQLite. Der Resolver liefert zunächst Admin, danach ReadOnly: `Forbidden`, kein Workbranch, Status weiterhin Running und gespeicherter Beleg erhalten. Keine automatisch behauptete terminale Fehlerbehandlung; diese gehört zum offenen Host-Dispatch.

Ausgeführt: `VERTEXBPMN_TEST_GIT` auf installierte Git-Datei gesetzt; `dotnet test tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --filter-not-class '*PostgresAcceptanceTests' --max-parallel-test-modules 1`. Ergebnis: **74 bestanden, 0 Fehler, 0 übersprungen**, 42,399 Sekunden; abhängige Projekte kompiliert, bestehende Warnungen bleiben. PostgreSQL, zentrale CI-safe Gesamtsuite und echter Identity-/GitHub-Hostpfad in diesem Nachtrag nicht erneut ausgeführt. Der oben dokumentierte Gesamtlauf gilt für den vorherigen Kandidaten.

- Kein automatisch laufender Commit-BackgroundService und noch keine Source-Control-API. Der endliche Executor ist per DI verfügbar, nimmt aber eine bereits vertrauenswürdig aufgelöste Identität und aktuelle Rollen entgegen. Der Host muss Rollen und Benutzerstatus verlässlich neu prüfen; gespeicherte Browserrollen oder pauschales Admin sind kein Ersatz. Claim-Dispatch, Heartbeat und Fehler-/Cancelzustände müssen daran angeschlossen werden.
- Der Produktionspfad mit echtem GitHub-App-Token ist nicht als kompletter gehosteter Worker abgenommen. Native Executor-Abnahmen nutzen signierte vorbereitete lokale Workspaces; Transport separat über echten lokalen HTTPS-Server. Keine externe GitHub-Schreibabnahme behauptet.
- Reconciliation nach lokalem Prozess-/Leaseverlust ist belegt, aber nicht nach dauerhaftem Verlust des Workspace-Datenträgers oder Failover auf einen Host ohne diesen Workspace. Keine verteilte Dateisystemverfügbarkeit behauptet.
- Push mit erwarteter Remote-Ref samt verlorenem Response, vollständige Read-/History-/Diff-Providerpfade, API/UI, GitHub-Review und Deployment-Provenienz bleiben offen.
- G02/G03/G04 bleiben insgesamt offen; keine ursprünglichen Pflichtkriterien gestrichen oder als bestanden markiert.
