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

Berichte liegen unter `tests/VertexBPMN.Studio.UiTests/TestResults/production-readiness/<runId>/`. `summary.json` enthält Status, Commit, SDK, Zeitpunkte, gezählte Testfälle und nach dem Build Artefakthashes. Testgruppen werden auch bei Fehlschlag mit Dauer sowie Total/Passed/Failed/Skipped/Errors erfasst; bei fehlendem Bericht bleiben die Zählwerte unbekannt (`null`). Logs und XML-Ergebnisse sind lokale Diagnoseartefakte; vor Weitergabe auf sensible Inhalte prüfen, insbesondere Browser-Traces und XML-Testfehler. Die Konsolenlog-Redaktion ersetzt keine vollständige Inhaltsprüfung.

- Fehlender Bericht, null Tests, nicht bestandene/übersprungene Fälle, unvollständige Zählung oder fehlende benannte Pflichtmethode verhindern Erfolg.
- Eine fehlgeschlagene Stufe bricht ab; nachfolgende Stufen gelten nicht als geprüft.
- Ein sauberer Checkout wird am Anfang und Ende geprüft. Der Runner erstellt keine Commits und keine Veröffentlichung.
- `AcceptancePassed` besagt, dass diese lokalen Tests bestanden wurden. Es ersetzt keine Sicherheits-, Last-, Restore- oder Zielumgebungsabnahme aus den späteren Planphasen.
- Der Infrastruktur-Start ist kein Health-Nachweis; externe Tests müssen tatsächliche Datenbank-/Brokeroperationen bestehen.
- `scripts/test-readiness-report.ps1` prüft die Report-Gates unabhängig von PostgreSQL oder Browsern.
- `scripts/test-readiness-summary.ps1` prüft unabhängig drei Fälle der Zusammenfassung: Erfolg, Fehler-/Skip-Zählung und fehlender Bericht. Beide Skripte bestehen lokal.

Die lokale Testausführung benötigt die Berechtigung, API, Studio und Chromium als Unterprozesse zu starten. `spawn EPERM` beim Browserstart ist eine Host-Prozessbeschränkung, kein fehlgeschlagener GUI-Use-Case. Start- und Cleanup-Fehler werden gemeinsam erhalten, wenn beispielsweise eine defekte Datenbankverbindung auch die Bereinigung verhindert.

Die Auswahl basiert auf vorhandenen Projekt-Traits. Nicht mehr relevante übersprungene Tests dürfen nicht pauschal entfernt werden: zunächst Umfang und Zweck prüfen und eine explizite Entscheidung im Release-Profil dokumentieren.

## Implementierungsstand 2026-09-08

- Report-Gates: sieben lokale Checks erfolgreich; Dirty-Checkout-Sperre und Fehlerbericht geprüft.
- Frischer Solution-Restore deckte inkompatible OpenAPI-Pakete auf. `Microsoft.OpenApi` wurde auf 2.7.5 an die Abhängigkeitsgrenze von `Microsoft.AspNetCore.OpenApi 10.0.11` angepasst; anschließender Release-Build erfolgreich.
- Vier bislang fest übersprungene externe Tests sind nun bei gesetzten Verbindungsdaten ausführbar.
- Vollständige Release-Freigabe steht aus; Diagnoseergebnisse werden im Review-Plan fortgeschrieben.

### Erster vollständiger Kernlauf

Diagnoselauf `7cd89323cac9431499bec4e3f1d8979b`: 839 Tests, 831 bestanden, 7 fehlgeschlagen, 1 übersprungen. Runner brach korrekt ab; externe und Browser-Stufen wurden dadurch nicht erreicht.

Fünf Fehler betrafen positive API-/SDK-Fixtures mit unverbundenem Start-/Endevent (PollingTrigger, TestRun, WorkflowTrigger und zwei SDK-Fälle). Diese wurden mit einem Sequence Flow verbunden; fachliche Erwartungen bleiben erhalten. Der einzige Skip betraf einen bereits HTTP-gemockten OpenAI-Fehlertest und wurde entfernt. Die Korrekturen sind durch den nachfolgenden Kernlauf verifiziert.

Zwei weitere Fehler betrafen Transaction-Cancel/Kompensation (`FPS_COMPENSATION_04`, `FPS_BPMN_09`); die Parser-Ursache ist im Folgestand beschrieben. Der Deploymentfehler im ersten Test gibt jetzt den Response-Body aus. Keine fachliche Assertion abgeschwächt.

### Verifizierter Folgestand

Die damalige Build-Sperre ist überwunden. Der Parser behandelte `<transaction>` nicht als Subprozess, obwohl die nachgelagerte Logik Transaktionen bereits vorsah. Der fehlende Parser-Zweig wurde ergänzt und durch einen Scope-/Cancel-Boundary-Regressionstest abgesichert; die fachlichen Cancel-/Kompensationsprüfungen wurden nicht abgeschwächt.

- Diagnoselauf `b588b93985b948cebe2af19aca49e7dc`: Release-Restore/Build erfolgreich; Kern **840 bestanden**, externe PostgreSQL-/RabbitMQ-Verträge **7 bestanden**, jeweils keine Fehler oder Skips.
- Browser-Nachlauf `tests/VertexBPMN.Studio.UiTests/TestResults/phase2-browser.xml`: **61 bestanden**, keine Fehler oder Skips. Der Kontrasttest wartet vor der Messung auf Blazor-Interaktivität; die WCAG-Schwelle bleibt unverändert. Die zwei lokalen Opt-in-Gruppen werden vom Abnahmerunner explizit aktiviert.
- Reale GUI-E2E: erster Lauf `a97b471bfb074dd985ffcf05e84c0665` scheiterte bei der gemeinsamen API-Initialisierung an einem PostgreSQL-Authentifizierungs-Timeout. **65 fehlgeschlagene Fälle sind kein Nachweis einzelner GUI-Defekte**, da ihre Testkörper nicht erreicht wurden. Die Startursache und anschließende Gesamtausführung werden weiter geprüft.

Diese getrennten Nachläufe ersetzen noch keinen erfolgreichen Gesamtlauf gegen den finalen Kandidaten. Phase 2 ist nicht abgeschlossen.

### Reale GUI-Nachprüfung

Mit einem frischen temporären PostgreSQL-Container im benannten WSLC-Netzwerk konnte Lauf `250af42d83024df9bd8de858ebadd8a3` API, Studio und alle GUI-Testkörper ausführen: **97 Tests, 91 bestanden, 6 fehlgeschlagen, 0 übersprungen, 0 Runner-Fehler**. Die 65 zunächst entdeckten Fälle wurden durch Theory-Daten erweitert. Alle fünf isolierten Datenbanken wurden anschließend erfolgreich entfernt und ihre Abwesenheit geprüft. Der Zusammenhang mit dem Netzwerkpfad ist ein Diagnosehinweis, kein abschließender WSLC-Fehlernachweis.

Die sechs Fehler waren veraltete Bedienannahmen: fehlende Sequence-Flow-Auswahl vor Katalogeinfügung, veralteter Upload-Text (zwei Tests), veralteter Leerzustand-Text (zwei Tests) und nicht geöffneter Reiter „Test runs“. Die Tests bedienen nun die tatsächliche Oberfläche; Persistenz- und Engine-Assertions bleiben erhalten. Der Upload wird genau einmal nach Blazor-Interaktivität ausgeführt, statt einen verlorenen Upload durch Wiederholung zu verdecken.

Nachlauf `bbae721bcea94e3eace0b2b4dff61e02`: fünf dieser sechs Fälle bestanden. Der verbleibende Reload-Test deckte einen reihenfolgeabhängigen Testaufbau auf: Der Testmandant wurde erst nach Initialisierung der Mandantenauswahl angelegt. Die betroffenen Tests legen ihn nun vor der Seitennavigation an. Isolierter Nachlauf `e2a8c1ea8e924e2995a938581e0b3963`: **beide Tenant-/Suchtests bestanden**, keine Skips oder Cleanup-Fehler.

Der lokale Starter akzeptiert für gezielte Nachläufe mehrere `-TestMethod`-Namen als PowerShell-Array. Null entdeckte Tests oder ein fehlender Report werden weiterhin nicht als Erfolg gewertet.

### Erfolgreiche gebündelte Diagnose

Lauf `0dc01e3d9f9e40ec9af05abbd0412dbd`, 2026-09-08 18:15:15–18:32:21 UTC (etwa 17 Minuten), Status **DiagnosticPassed**, Exitcode 0. Basiscommit `e4b39490b06d01f0fe319368750ef5c1b009f097` mit uncommitteten Phase-2-Korrekturen. Aufruf gegen ausschließlich temporäre WSLC-Dienste; Zugangsdaten wurden separat bereitgestellt:

```powershell
./scripts/test-production-readiness.ps1 -Infrastructure Existing -PostgresPort 55440 -RabbitMqPort 55679 -AllowDirty
```

| Gruppe | Bestanden | Fehlgeschlagen / übersprungen | Dauer |
| --- | ---: | --- | ---: |
| Kern | 840 | 0 / 0 | 238 s |
| PostgreSQL-/RabbitMQ-Verträge | 7 | 0 / 0 | 11 s |
| Browser-Verträge und lokale Opt-ins | 61 | 0 / 0 | 257 s |
| Reale Studio-E2E | 97 | 0 / 0 | 466 s |
| **Gesamt** | **1.005** | **0 / 0** | |

Alle Gruppen haben zusätzlich 0 Runner-Fehler. SDK 10.0.303; PostgreSQL 17.11 und RabbitMQ 4.3.5 sind aus den echten Verbindungen im externen XML-Testbericht protokolliert. Release-Restore/Build erfolgreich, 0 Buildfehler und 17 Warnungen. Der Bericht enthält die SHA-256-Hashes der API-, Studio- und Testassemblies. Alle fünf isolierten E2E-Datenbanken wurden nachweislich entfernt. Zusätzlich bestehen sieben Report-Gate- und drei Summary-Prüfungen.

Nachweise liegen unter `tests/VertexBPMN.Studio.UiTests/TestResults/production-readiness/0dc01e3d9f9e40ec9af05abbd0412dbd/`: `summary.json`, Gruppen-XML/-Logs sowie `e2e/results.html`, Screenshots, Traces und `e2e/database-cleanup.log`.

**Noch offen:** Korrekturen committen und die Abnahme ohne `-AllowDirty` aus dem sauberen finalen Checkout wiederholen. Der erfolgreiche Diagnoselauf ist weder ein unveränderlicher Release-Kandidat noch eine Freigabe der Sicherheits-, Ausfall-, Restore- oder Lastphasen. Die temporären Testdienste werden nach der Prüfung entfernt; die genannten Testports sind keine dauerhafte lokale Konfiguration.

### Finale Abnahme ab sauberem Checkout (Server/Linux)

Lauf `9aed5f0ecc074544b90945644edcfb10`, 2026-09-08, Status **AcceptancePassed**, Exitcode 0. Aufruf vom **sauberen Checkout** `3805236` (keine uncommitteten Änderungen, kein `-AllowDirty`) gegen vorhandene lokale PostgreSQL-/RabbitMQ-Dienste:

```powershell
~/pwsh/pwsh -NoProfile -Command "& ./scripts/test-production-readiness.ps1 -Infrastructure Existing -PostgresHost 127.0.0.1 -PostgresPort 55432 -RabbitMqHost 127.0.0.1 -RabbitMqPort 55672 -User vertexbpmn -Password <pw>"
```

| Gruppe | Bestanden | Fehlgeschlagen / übersprungen |
| --- | ---: | --- |
| Kern | 840 | 0 / 0 |
| PostgreSQL-/RabbitMQ-Verträge | 7 | 0 / 0 |
| Browser-Verträge und lokale Opt-ins | 61 | 0 / 0 |
| Reale Studio-E2E | 97 | 0 / 0 |
| **Gesamt** | **1.005** | **0 / 0** |

SDK 10.0.302; Artefakt-SHA-256 (API, Studio, Tests, UiTests-DLLs) im `summary.json`. Alle fünf isolierten E2E-Datenbanken nachweislich entfernt; Arbeitsbaum danach sauber. **Reproduzierbarkeitshinweis:** Ein erster Lauf flakte in der Browser-Stufe mit 4 Fehlern (Studio-Bootstrap-Timeouts, `ERR_CONNECTION_REFUSED`, `Failed to fetch`) unter Volllast der 61-Test-Gruppe; alle 4 passierten isoliert und im Folgegesamtlauf (61/61). Das ist Harness-/Server-Start-Konkurrenz, kein Produktdefekt — bei einer nicht bestandenen Browser-Stufe zuerst gezielt wiederholen bzw. das Server-Startfenster prüfen.
