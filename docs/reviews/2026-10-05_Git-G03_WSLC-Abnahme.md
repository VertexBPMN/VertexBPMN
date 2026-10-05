# G03 – lokale PostgreSQL-Abnahme mit WSLC

Stand: 2026-10-05. Phase 2 bleibt offen; dieser Nachweis betrifft die bereits implementierte Persistenzbasis, nicht die vollständige Git-Integration.

Fortsetzung: zusätzlicher tatsächlicher WSLC-Datenbankneustart, Helper-/Git-/HTTPS-Adapterprüfungen, Lease-/Quota-/Retention-Ergänzungen und finale Regression im [aktuellen Phase-2-Abschlussstand](2026-10-05_Git-Phase2_Abschlussstand.md). Ergebnisse unten dokumentieren den früheren Teilstand und werden nicht als neue Ausführung ausgegeben.

## Tatsächlich ausgeführt

- Isolierter WSLC-Container `vertex-source-control-phase2-pg`, vorhandenes Image `postgres:17-alpine`, PostgreSQL 17.11. Veröffentlichung ausschließlich `127.0.0.1:55439` durch `wslc inspect` bestätigt. Passwortlose Trust-Authentifizierung ausschließlich für diese kurzlebige Loopback-Testinstanz; keine Produktkonfiguration geändert.
- `tests/VertexBPMN.SourceControl.Tests/PostgresAcceptanceTests.cs`: echter PostgreSQL-Test, **1 bestanden, 0 fehlgeschlagen, 0 übersprungen**. Upgrade vom Stand `20260912090238_ExternalTaskPersistence`, persistente Repositorybindung und ACL, Idempotenz über getrennte DbContexts, konkurrierende Claims mit genau einem Gewinner, Lease-Ablauf mit erhöhtem Fence und `Reconciling`, veralteter Worker darf nicht abschließen. Frischer DbContext ist kein Nachweis eines Betriebssystem-Prozessneustarts.
- Der erste Lauf mit direktem `UseNpgsql` verwendete nicht die produktive Providerkonfiguration; korrigiert auf vorhandenes `UseVertexNpgsql`. Danach fand der unveränderte Test einen echten Fehler: `text = uuid`. Ursache waren SQLite-Typinformationen des gemeinsamen Snapshots. Die neue, noch nicht veröffentlichte Migration `20261005131442_SourceControlPersistence` verwendet nun explizite Typen je Provider. Keine bereits veröffentlichte Migration verändert, keine Assertion abgeschwächt.
- `SourceControlPersistenceTests`: **5 bestanden, 0 fehlgeschlagen, 0 übersprungen** nach der Migrationskorrektur; SQLite bleibt kompatibel.
- Anschließend unveränderte CI-safe Suite gemäß `.github/workflows/ci.yml`: **1.374 gesamt, 1.366 bestanden, 0 fehlgeschlagen, 8 bestehende Infrastruktur-/Live-Opt-in-Fälle übersprungen**, Exit 0, 93,991 s. Übersprungene Fälle sind keine bestandene externe Abnahme.
- Lösungsbuild vor der Migrationskorrektur: **0 Fehler, 68 Warnungen**. Beide betroffenen Testprojekte wurden nach der Korrektur erneut erfolgreich gebaut.

## Wiederholung – ausschließlich lokal

Eine isolierte PostgreSQL-Adminverbindung im Prozess als `VERTEXBPMN_TEST_POSTGRES_ADMIN` setzen, ohne Zugangsdaten ins Repository oder Chat zu schreiben. Der Test erstellt eine eindeutig benannte Datenbank und entfernt genau diese im `finally` wieder.

```powershell
dotnet test tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --no-restore --filter-class '*PostgresAcceptanceTests' --max-parallel-test-modules 1
```

Der Test liegt im separaten Source-Control-Testprojekt, nicht in der zentralen CI-safe Suite. GitHub-Workflow und dessen Filter wurden nicht verändert. Ohne explizite Verbindung schlägt die lokale Abnahme verständlich fehl, statt Infrastrukturprüfung als bestanden auszugeben.

## Noch kein Abschlussnachweis

Aufräumen bestätigt: ausschließlich der in diesem Lauf angelegte Container und sein durch `inspect` zugeordnetes anonymes Volume entfernt. `wslc ps` anschließend leer. Keine vorhandenen Images, Volumes oder Produktdaten entfernt. Ein Testbuild regenerierte Editorbundles; ausschließlich diese eigenen, aufgabenfremden Änderungen wurden wieder auf den vorherigen Stand zurückgesetzt.

SQL Server nicht ausgeführt. Kein echter GitHub-App-/Remote-Git-Write, kein nativer Git-Adapter-End-to-End-Test und kein Browsertest. Native Prozessgrenzen, Tokenkanal-Abnahme, atomare Quoten, Lease-Erneuerung, Recovery-Orchestrierung und Workspace-Cleanup-Abnahme bleiben Pflichtarbeit für Phase 2. Ein bestandener Datenbanktest bedeutet nicht, dass die Git-Funktion bereits end-to-end nutzbar ist.
