# Lokale Release-Abnahme

`scripts/test-production-readiness.ps1` bündelt Restore, Release-Build, Kernsuite, externe PostgreSQL-/RabbitMQ-Verträge, Browser-Verträge und reale Studio-E2E. GitHub-Workflows bleiben unverändert.

## Aufruf

Aus einem sauberen Checkout des zu prüfenden Commits:

```powershell
./scripts/test-production-readiness.ps1 -Infrastructure Wslc
```

Vorhandene lokale Dienste:

```powershell
./scripts/test-production-readiness.ps1 -Infrastructure Existing -PostgresPort 55432 -RabbitMqPort 55672
```

Zugangsdaten über `VERTEXBPMN_WSLC_PASSWORD` oder Parameter bereitstellen, nicht committen. Nur lokale dedizierte Testdienste verwenden: Tests erstellen Datenbanken, Broker-Ressourcen und Testdaten. WSLC-Modus verwendet den vorhandenen Infrastrukturstarter; Existing startet keine Dienste. Für Entwicklung ist `-AllowDirty` möglich; selbst ein vollständig grüner Lauf erhält dann nur `DiagnosticPassed`.

## Auswertung

Berichte liegen unter `tests/VertexBPMN.Studio.UiTests/TestResults/production-readiness/<runId>/`. `summary.json` enthält Status, Commit, SDK, Zeitpunkte, gezählte Testfälle und nach dem Build Artefakthashes. Logs und XML-Ergebnisse sind lokale Diagnoseartefakte; vor Weitergabe auf sensible Inhalte prüfen, insbesondere Browser-Traces und XML-Testfehler. Die Konsolenlog-Redaktion ersetzt keine vollständige Inhaltsprüfung.

- Fehlender Bericht, null Tests, nicht bestandene/übersprungene Fälle, unvollständige Zählung oder fehlende benannte Pflichtmethode verhindern Erfolg.
- Eine fehlgeschlagene Stufe bricht ab; nachfolgende Stufen gelten nicht als geprüft.
- Ein sauberer Checkout wird am Anfang und Ende geprüft. Der Runner erstellt keine Commits und keine Veröffentlichung.
- `AcceptancePassed` besagt, dass diese lokalen Tests bestanden wurden. Es ersetzt keine Sicherheits-, Last-, Restore- oder Zielumgebungsabnahme aus den späteren Planphasen.
- Der Infrastruktur-Start ist kein Health-Nachweis; externe Tests müssen tatsächliche Datenbank-/Brokeroperationen bestehen.
- `scripts/test-readiness-report.ps1` prüft die Report-Gates unabhängig von PostgreSQL oder Browsern.

Die Auswahl basiert auf vorhandenen Projekt-Traits. Nicht mehr relevante übersprungene Tests dürfen nicht pauschal entfernt werden: zunächst Umfang und Zweck prüfen und eine explizite Entscheidung im Release-Profil dokumentieren.

## Implementierungsstand 2026-09-08

- Report-Gates: sieben lokale Checks erfolgreich; Dirty-Checkout-Sperre und Fehlerbericht geprüft.
- Frischer Solution-Restore deckte inkompatible OpenAPI-Pakete auf. `Microsoft.OpenApi` wurde auf 2.7.5 an die Abhängigkeitsgrenze von `Microsoft.AspNetCore.OpenApi 10.0.11` angepasst; anschließender Release-Build erfolgreich.
- Vier bislang fest übersprungene externe Tests sind nun bei gesetzten Verbindungsdaten ausführbar.
- Vollständige Release-Freigabe steht aus; Diagnoseergebnisse werden im Review-Plan fortgeschrieben.

### Erster vollständiger Kernlauf

Diagnoselauf `7cd89323cac9431499bec4e3f1d8979b`: 839 Tests, 831 bestanden, 7 fehlgeschlagen, 1 übersprungen. Runner brach korrekt ab; externe und Browser-Stufen wurden dadurch nicht erreicht.

Fünf Fehler betreffen positive API-/SDK-Fixtures mit unverbundenem Start-/Endevent (PollingTrigger, TestRun, WorkflowTrigger und zwei SDK-Fälle). Diese wurden mit einem Sequence Flow verbunden; fachliche Erwartungen bleiben erhalten. Der einzige Skip betrifft einen bereits HTTP-gemockten OpenAI-Fehlertest und wurde entfernt. Diese Änderungen sind noch nicht erneut verifiziert.

Zwei weitere Fehler betreffen Transaction-Cancel/Kompensation (`FPS_COMPENSATION_04`, `FPS_BPMN_09`); Ursache noch offen. Der Deploymentfehler im ersten Test gibt jetzt den Response-Body aus. Keine fachliche Assertion abgeschwächt.

Der anschließende Testbuild wurde von der automatischen Freigabeprüfung wegen fehlender Workspace-Credits abgelehnt. Nächster Schritt: Build und gezielter Lauf der sieben Fehlerfälle plus OpenAI-Fehlertest, danach fachliche Transaction-Cancel-Ursache korrigieren und Gesamtabnahme wiederholen. Phase 2 ist nicht abgeschlossen.
